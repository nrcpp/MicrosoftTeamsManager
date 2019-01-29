using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft_Teams_Graph_RESTAPIs_Connect.Auth;
using Microsoft_Teams_Graph_RESTAPIs_Connect.ImportantFiles;
using Microsoft_Teams_Graph_RESTAPIs_Connect.Models;
using Siemplify.Common.ExternalChannels.DataModel;
using Siemplify.Common.ExternalChannels.Utils;

namespace Siemplify.Common.ExternalChannels
{
    public class MsTeamsChannelProvider : IExternalChannelProvider, IExternalChannelProviderAsync
    {        
        readonly GraphService graphService = new GraphService();
        private string _token;

        public string CurrentTeamId { get; set; }       // set team id before any call. Will be set on Connect() to first team.

        public string Token => GetTokenAsync().Result;

        public FormOutput LastResult { get; private set; }


        public MsTeamsChannelProvider()
        {

        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");


        // obtains on Connect()  
        private async Task<string> GetTokenAsync()
        {
            _token = await AuthProvider.Instance.GetUserAccessTokenAsync().ConfigureAwait(false);
            graphService.accessToken = _token;
            return _token;
        }
        

        private async Task<FormOutput> WithExceptionHandling(Func<string, FormOutput> call, [CallerMemberName] string callerName = "")
        {
            return await WithExceptionHandlingAsync(
                async s => call(s),
                callerName);
        }

        private async Task<FormOutput> WithExceptionHandlingAsync(Func<string, Task<FormOutput>> call, [CallerMemberName] string callerName = "")
        {
            FormOutput output = new FormOutput();

            try
            {
                // Configuration settings have to be set
                if (ConfigurationManager.AppSettings["ida:AppId"] == null
                    || ConfigurationManager.AppSettings["ida:AppSecret"] == null)
                {
                    Log("You need to put your appid and appsecret in Web.config.secrets. See README.md for details.");
                    return output;
                }

                // Get an access token.
                string accessToken = await AuthProvider.Instance.GetUserAccessTokenAsync();
                graphService.accessToken = accessToken;
                output = await call(accessToken);

                output.Action = callerName.Replace("Form", "Action");

                output.UserUpn = await graphService.GetMyId(accessToken);

                if (output.ShowTeamDropdown)
                    output.Teams = (await graphService.GetMyTeams(accessToken)).ToArray();

                if (output.ShowGroupDropdown)
                    output.Groups = (await graphService.GetMyGroups(accessToken)).ToArray();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }

            return output;
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
            CurrentTeamId = (await graphService.GetMyTeams(Token)).FirstOrDefault()?.id;
        
        #endregion


        #region IExternalChannelProviderAsync implementation

        // Provider name
        public string Provider => nameof(MsTeamsChannelProvider);


        // See also: AuthProvider\Startup.Auth.cs               
        public async Task ConnectAsync()
        {
            LastResult = await WithExceptionHandling(
                func => new FormOutput()
                {
                    ShowTeamDropdown = true,
                }
            );
            
            CurrentTeamId = LastResult.Teams?.FirstOrDefault()?.id;     // NOTE: select first team on connection
        }


        // Channels
        private async Task<FormOutput> CreateChannelInternal(string channelName, string channelDescription)
        {
            await graphService.CreateChannel(Token,
                        CurrentTeamId, channelName, channelDescription);
            var channels = (await graphService.GetChannels(Token, CurrentTeamId)).ToArray();

            LastResult = new FormOutput()
            {
                Channels = channels,
                ShowChannelOutput = true
            };

            return LastResult;
        }


        public async Task<bool> CreateChannelAsync(string channelName, List<string> channelUsers)
        {
            var response = await CreateChannelInternal(channelName, channelDescription: "");

            var channel = response?.Channels?.FirstOrDefault(ch => ch.displayName == channelName);
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
            return (await graphService.GetChannels(Token, CurrentTeamId)).ToArray();
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
            var teams = await graphService.GetMyTeams(Token);
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
                await graphService.HttpPost($"/groups/{teamToAddTo.id}/members/$ref", payload);
            }
            else
                await graphService.HttpDelete($"/groups/{teamToAddTo.id}/members/{user.UserId}/$ref", GraphService.GraphV1Endpoint);

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
            var response = await graphService.HttpDelete($"/teams/{CurrentTeamId}/channels/{channel.id}", HttpHelpers.GraphBetaEndpoint);

            Log(response);
        }



        public async Task<List<ChannelUser>> GetAllUsersAsync(string userPrefix = "")
        {
            var users = await graphService.GetUsers();
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
            var messages = await graphService.HttpGet<HistoryMessages>($"/teams/{CurrentTeamId}/channels/{channel.id}/messages", GraphService.GraphBetaEndpoint);
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

            await graphService.PostMessage(Token, CurrentTeamId, channelId, message);

            LastResult = new FormOutput()
            {
                SuccessMessage = "Done",
            };
        }

        #endregion


        #region IExternalChannelProvider implementation. Synchronous methods.

        private async Task<T> CallWithSyncResult<T>(Func<Task<T>> asyncFunc) 
        {
            var result = await asyncFunc().ConfigureAwait(false);
            return result;
        }


        // obtains on Connect()  
        private async Task<ChannelUser> AwaitAddUserToChannel(string channelName, string userName) =>
            await AddUserToChannelAsync(channelName, userName).ConfigureAwait(false);

        public ChannelUser AddUserToChannel(string channelName, string userName) =>        
            AsyncHelpers.RunSync(() => AddUserToChannelAsync(channelName, userName));        
        

        public bool CreateChannel(string channelName, List<string> channelUsers)
        {
            throw new NotImplementedException();
        }

        public List<ChannelUser> GetAllUsers(string userPrefix = "")
        {
            throw new NotImplementedException();
        }

        public List<ChannelUser> GetChannelUsers(string channelName)
        {
            throw new NotImplementedException();
        }

        public List<ChannelMessage> GetMessages(string channelName, DateTime? from)
        {
            throw new NotImplementedException();
        }

        public List<ChannelMessage> GetMessages(string channelName)
        {
            throw new NotImplementedException();
        }

        public void RemoveUserFromChannel(string channelName, string userName)
        {
            throw new NotImplementedException();
        }

        public void SendMessage(string channelName, string message)
        {
            throw new NotImplementedException();
        }

        public void Connect()
        {
            throw new NotImplementedException();
        }

        public void CloseChannel(string channelName)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}