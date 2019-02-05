using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using MSTeamsManager.Helpers;
using MSTeamsManager.Models;
using Newtonsoft.Json.Linq;
using Siemplify.Common.ExternalChannels.DataModel;
using Siemplify.Common.ExternalChannels.Utils;

namespace Siemplify.Common.ExternalChannels
{
    public class MsTeamsChannelProvider : IExternalChannelProvider, IExternalChannelProviderAsync
    {        
        readonly GraphService _graphService = new GraphService();
        

        // Don't change this constant
        // It is a constant that corresponds to fixed values in AAD that corresponds to Microsoft Graph
        //
        // Required Permissions - Microsoft Graph -> API
        // Read all users' full profiles
        // Read and write all groups
        const string aadResourceAppId = "00000003-0000-0000-c000-000000000000";


        public string CurrentTeamId { get; set; }       // set team id before any call. Will be set on Connect() to first team.

        public string Token { get; private set; }

        

        public MsTeamsChannelProvider()
        {
            _authConfig = AuthenticationConfig.ReadFromJsonFile("appsettings.json");

            // NOTE: For testing purposes on my machine
#if DEBUG
            if (Environment.MachineName == "PC") _authConfig = AuthenticationConfig.ReadFromJsonFile("appsettings-dk-machine.json");
#endif
        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) => Console.WriteLine($"[{caller}]: {msg}");


        private ChannelUser ToChannelUser(User user)
        {
            return new ChannelUser()
            {
                UserId = user.id,

                FullName = user.displayName,
                Picture = "",               // NOTE: no field in response for this
            };
        }


        private ChannelMessage ToChannelMessage(HistoryMessages.Message m, Channel cannnel)
        {
            return new ChannelMessage()
            {
                Time = m.createdDateTime,
                User = m.from?.user?.id,
                Username = m.from?.user?.displayName,
                Text = m.body?.content,
                ChannelId = cannnel.id,
            };
        }


        public async Task SelectFirstTeam()
        {
            CurrentTeamId = (await _graphService.GetMyTeams(Token)).FirstOrDefault()?.id;
            if (CurrentTeamId == null)
                Log("Warning: no teams found for current user. CurrentTeamId is null.");
        }

        internal Team[] GetMyTeams() => AsyncHelpers.RunSync(() => _graphService.GetMyTeams(Token));

        #endregion


        #region IExternalChannelProviderAsync implementation

        // Provider name
        public string Provider => nameof(MsTeamsChannelProvider);


        // Connect / Login
        AuthenticationContext _authenticationContext ;
        AuthenticationResult _authenticationResult;
        AuthenticationConfig _authConfig;


        private async Task<AuthenticationResult> TryFetchTokenSilently()
        {
            AuthenticationResult result = null;

            // first, try to get a token silently
            try
            {
                return result = await _authenticationContext.AcquireTokenSilentAsync(aadResourceAppId, _authConfig.ClientId);
            }
            catch (AdalException adalException)
            {
                // There is no token in the cache; prompt the user to sign-in.
                if (adalException.ErrorCode == AdalError.FailedToAcquireTokenSilently
                    || adalException.ErrorCode == AdalError.InteractionRequired)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("No token in the cache");
                    return result;
                }

                Log("An unexpected error occurred.\r\n" + adalException);
            }

            return result;
        }
        

        private async Task<AuthenticationResult> UserLogin()
        {
            Debug.Assert(_authConfig != null);

            var authority = string.Format(CultureInfo.InvariantCulture, _authConfig.AadInstance, _authConfig.Tenant);
            _authenticationContext = new AuthenticationContext(authority, new FileCache());
            AuthenticationResult result = await TryFetchTokenSilently();

            if (result == null)
            {
                UserCredential uc = ConsoleUtils.TextualPrompt();

                // if you want to use Windows integrated auth, comment the line above and uncomment the one below
                // UserCredential uc = new UserCredential();
                try
                {
                    // NOTE: that this type of auth is working for NATIVE client apps. 
                    // Make sure your App was registered as Native, not Web on Azure Active Directory Apps.
                    result = await _authenticationContext.AcquireTokenAsync(aadResourceAppId, _authConfig.ClientId, uc);
                }
                catch (Exception ex)
                {
                    Log("AcquireTokenAsync Failed. Make sure your App was registered as Native at Azure Active Directory Apps:\r\n\t" +
                        "https://portal.azure.com/#blade/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/RegisteredApps\r\n\t" + ex.Message);                    
                }
            }

            return result;

            // Uncomment to Login through Browser window:
            //_authenticationContext.TokenCache.Clear();
            //DeviceCodeResult deviceCodeResult = _authenticationContext.AcquireDeviceCodeAsync(aadResourceAppId, _authConfig.ClientId).Result;
            //Log(deviceCodeResult.Message);
            //return _authenticationContext.AcquireTokenByDeviceCodeAsync(deviceCodeResult).Result;
        }


        public async Task ConnectAsync()
        {
            Token = CurrentTeamId = null;
            
            _authenticationResult = await UserLogin();
            if (string.IsNullOrEmpty(_authenticationResult.AccessToken))
            {
                Log("Login failed. Token is empty.");
                return;
            }
            else            
                Log("You've successfully signed in as " + _authenticationResult.UserInfo.DisplayableId);

            Token = _graphService.accessToken = _authenticationResult.AccessToken;

            // select first team to process other requests for it
            await SelectFirstTeam();
        }



        // Channels        

        public async Task<bool> CreateChannelAsync(string channelName, List<string> channelUsers)
        {
            await _graphService.CreateChannel(Token,
                       CurrentTeamId, channelName, channelDescription: "");

            var channelList = await _graphService.GetChannels(Token, CurrentTeamId);

            var channel = channelList.FirstOrDefault(ch => ch.displayName == channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel was not created");
                return false;
            }
            else
                Log($"{channelName} - channel created");

            if (channelUsers == null) return true;

            bool result = true;            
            foreach (var user in channelUsers)
                result &= (await AddUserToChannelAsync(channelName, user)) != null;

            return result;
        }

        public async Task<Channel[]> GetChannels()
        {
            return (await _graphService.GetChannels(Token, CurrentTeamId)).ToArray();
        }

        private async Task<Channel> GetChannelByName(string name) =>
            (await GetChannels())?.FirstOrDefault(ch => ch.displayName == name);

        public async Task<Channel> GetChannelById(string id) =>
            (await GetChannels())?.FirstOrDefault(ch => ch.id == id);


        // users
        public async Task<ChannelUser> GetUserByFullName(string name) =>
            (await GetAllUsersAsync()).FirstOrDefault(u => u.FullName == name);



        // Add/remove to channel
        private async Task<ChannelUser> AddOrRemoveUserInTeam(string teamName, string userName, bool add)
        {
            // find team
            var teams = await _graphService.GetMyTeams(Token);
            var teamToAddTo = teams.FirstOrDefault(t => t.displayName == teamName);
            if (teamToAddTo == null)
            {
                Log($"{teamToAddTo} - team not found");
                return null;
            }

            // add user to channel call
            var user = await GetUserByFullName(userName);
            if (user == null)
            {
                Log($"{userName} - user not found");
                return null;
            }            

            if (add)
            {
                string payload = $"{{ '@odata.id': '{GraphService.GraphBetaEndpoint}/users/{user.UserId}' }}";
                await _graphService.HttpPost($"/groups/{teamToAddTo.id}/members/$ref", payload);
            }
            else
                await _graphService.HttpDelete($"/groups/{teamToAddTo.id}/members/{user.UserId}/$ref", GraphService.GraphV1Endpoint);

            return user;
        }


        // NOTE: Add user to team. Every user in team could attend any channel        
        public async Task<ChannelUser> AddUserToChannelAsync(string teamName, string userName) =>
            await AddOrRemoveUserInTeam(teamName, userName, add: true);


        // NOTE: No API to remove user from channel. Instead remove user from team.
        // See: https://docs.microsoft.com/en-us/graph/api/resources/teams-api-overview?view=graph-rest-1.0
        public async Task RemoveUserFromChannelAsync(string teamName, string userName) =>
            await AddOrRemoveUserInTeam(teamName, userName, add: false);
        

        public async Task CloseChannelAsync(string channelName)
        {
            var channel = await GetChannelByName(channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel not found");
                return;
            }


            // DELETE /teams/{id}/channels/{id}
            var response = await _graphService.HttpDelete($"/teams/{CurrentTeamId}/channels/{channel.id}", HttpHelpers.GraphBetaEndpoint);

            Log(string.IsNullOrEmpty(response) ? "OK" : response);
        }



        public async Task<List<ChannelUser>> GetAllUsersAsync(string userPrefix = "")
        {
            var users = await _graphService.GetUsers();
            if (!string.IsNullOrEmpty(userPrefix))
                users = users.Where(u => u.displayName?.StartsWith(userPrefix) == true).ToArray();

            return users.Select(u => ToChannelUser(u)).ToList();
        }

        // All users in Team could participate in any channel
        // See: https://docs.microsoft.com/en-us/microsoftteams/teams-channels-overview
        public async Task<List<ChannelUser>> GetChannelUsersAsync(string channelName) => await GetAllUsersAsync();
        

        // Messages
        public async Task<List<ChannelMessage>> GetMessagesAsync(string channelName, DateTime? from)
        {
            // 1. getchannel
            var channel = await GetChannelByName(channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel not found");
                return null;
            }

            // 2. obtain messages
            
            // GET /teams/{id}/channels/{id}/messages
            var messages = await _graphService.HttpGet<HistoryMessages>($"/teams/{CurrentTeamId}/channels/{channel.id}/messages", GraphService.GraphBetaEndpoint);
            var result = messages.value.Select(m => ToChannelMessage(m, channel)).ToList();
            if (from != null)
                result = result.Where(m => m.Time >= from).ToList();

            return result;
        }


        public async Task<List<ChannelMessage>> GetMessagesAsync(string channelName) => await GetMessagesAsync(channelName, null);


        public async Task SendMessageAsync(string channelName, string message)
        {            
            var channelId = (await GetChannelByName(channelName))?.id;
            if (channelId == null)
                throw new ArgumentException("Channel not found - " + channelName);

            await _graphService.PostMessage(Token, CurrentTeamId, channelId, message);
        }

        #endregion


        #region IExternalChannelProvider implementation. Synchronous methods.

        public ChannelUser AddUserToChannel(string channelName, string userName) =>
            AsyncHelpers.RunSync(() => AddUserToChannelAsync(channelName, userName));

        public ChannelUser AddUserToTeam(string teamName, string userName) => AddUserToChannel(teamName, userName);     // alias for MS Teams

        public bool CreateChannel(string channelName, List<string> channelUsers) =>
            AsyncHelpers.RunSync(() => CreateChannelAsync(channelName, channelUsers));


        public List<ChannelUser> GetAllUsers(string userPrefix = "") =>
            AsyncHelpers.RunSync(() => GetAllUsersAsync(userPrefix));

        public List<ChannelUser> GetChannelUsers(string channelName) =>
            AsyncHelpers.RunSync(() => GetChannelUsersAsync(channelName));

        public List<ChannelUser> GetTeamUsers(string channelName) => GetChannelUsers(channelName);               // alias for MS Teams

        public List<ChannelMessage> GetMessages(string channelName, DateTime? from)
            => AsyncHelpers.RunSync(() => GetMessagesAsync(channelName, from));

        public List<ChannelMessage> GetMessages(string channelName) => AsyncHelpers.RunSync(() => GetMessagesAsync(channelName));


        public void RemoveUserFromChannel(string channelName, string userName)
            => AsyncHelpers.RunSync(() => RemoveUserFromChannelAsync(channelName, userName));

        public void RemoveUserFromTeam(string teamName, string userName) => RemoveUserFromChannel(teamName, userName);      // alias for MS Teams

        public void SendMessage(string channelName, string message)
            => AsyncHelpers.RunSync(() => SendMessageAsync(channelName, message));

        public void Connect()
            => AsyncHelpers.RunSync(() => ConnectAsync());

        public void CloseChannel(string channelName) => AsyncHelpers.RunSync(() => CloseChannelAsync(channelName));


        #endregion
    }
}