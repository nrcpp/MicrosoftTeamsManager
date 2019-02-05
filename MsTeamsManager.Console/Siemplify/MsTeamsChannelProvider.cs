using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
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
        private string _token;

        public string CurrentTeamId { get; set; }       // set team id before any call. Will be set on Connect() to first team.

        public string Token { get; private set; }

        

        public MsTeamsChannelProvider()
        {

        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");


        // obtains on Connect()  
        private async Task<string> GetTokenAsync()
        {
            // TODO: get token
            _token = "TODO: get token";  // await AuthProvider.Instance.GetUserAccessTokenAsync().ConfigureAwait(false);
            _graphService.accessToken = _token;
            return _token;
        }
        


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


        public async Task SelectFirstTeam() =>        
            CurrentTeamId = (await _graphService.GetMyTeams(Token)).FirstOrDefault()?.id;
        
        #endregion


        #region IExternalChannelProviderAsync implementation

        // Provider name
        public string Provider => nameof(MsTeamsChannelProvider);


        public async Task ConnectAsync()
        {
            Token = CurrentTeamId = "<uncknown>";

            AuthenticationConfig config = AuthenticationConfig.ReadFromJsonFile("appsettings.json");


            // NOTE: For testing purposes on my machine
#if DEBUG
            if (Environment.MachineName == "PC") config = AuthenticationConfig.ReadFromJsonFile("appsettings-dk.json");
#endif

            // Even if this is a console application here, a daemon application is a confidential client application
            ClientCredential clientCredentials;

#if !VariationWithCertificateCredentials
            clientCredentials = new ClientCredential(config.ClientSecret);
#else
            X509Certificate2 certificate = ReadCertificate(config.CertificateName);
            clientCredentials = new ClientCredential(new ClientAssertionCertificate(certificate));
#endif
            var app = new ConfidentialClientApplication(config.ClientId, config.Authority, "https://daemon", clientCredentials, null, new TokenCache());

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator
            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };

            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClientAsync(scopes);
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                throw;
            }

            if (result == null)
            {
                Log("Unable to AcquireTokenForClientAsync");
                return;
            }

            _graphService.accessToken = Token = result.AccessToken;
            CurrentTeamId = (await _graphService.GetMyTeams(Token)).FirstOrDefault()?.id;
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

            // TODO: add user to team if not exists
            bool result = true;
            //foreach (var user in channelUsers)
            //    result &= (await AddUserToChannel(channel, user)) != null;

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

            Log(response);
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
        

        public bool CreateChannel(string channelName, List<string> channelUsers) =>
            AsyncHelpers.RunSync(() => CreateChannelAsync(channelName, channelUsers));


        public List<ChannelUser> GetAllUsers(string userPrefix = "") =>
            AsyncHelpers.RunSync(() => GetAllUsersAsync(userPrefix));
        
        public List<ChannelUser> GetChannelUsers(string channelName) =>
            AsyncHelpers.RunSync(() => GetChannelUsersAsync(channelName));
        

        public List<ChannelMessage> GetMessages(string channelName, DateTime? from)
            => AsyncHelpers.RunSync(() => GetMessagesAsync(channelName, from));
        
        public List<ChannelMessage> GetMessages(string channelName) => AsyncHelpers.RunSync(() => GetMessagesAsync(channelName));


        public void RemoveUserFromChannel(string channelName, string userName)
            => AsyncHelpers.RunSync(() => RemoveUserFromChannelAsync(channelName, userName));
    
        public void SendMessage(string channelName, string message)
            => AsyncHelpers.RunSync(() => SendMessageAsync(channelName, message));

        public void Connect()
            => AsyncHelpers.RunSync(() => ConnectAsync());

        public void CloseChannel(string channelName) => AsyncHelpers.RunSync(() => CloseChannelAsync(channelName));

        #endregion
    }
}