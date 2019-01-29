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


namespace Siemplify.Common.ExternalChannels
{
    public class MsTeamsChannelProviderAsync : IExternalChannelProviderAsync
    {        
        readonly GraphService graphService = new GraphService();
        private string _token;

        public string CurrentTeamId { get; set; }


        public FormOutput LastResult { get; private set; }

        public MsTeamsChannelProviderAsync()
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

        private string Token => GetTokenAsync().Result;


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

        #endregion


        #region IExternalChannelProvider implementation

        // Provider name
        public string Provider => nameof(MsTeamsChannelProviderAsync);


        // See also: AuthProvider\Startup.Auth.cs               
        public async Task Connect()
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


        public async Task<bool> CreateChannel(string channelName, List<string> channelUsers)
        {
            var response = await CreateChannelInternal(channelName, channelDescription: "");

            var channel = response?.Channels?.FirstOrDefault(ch => ch.displayName == channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel was not created");
                return false;
            }

            bool result = true;
            foreach (var user in channelUsers)
                result &= (await AddUserToChannelInternal(channel, user)) != null;

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
            (await GetAllUsers()).FirstOrDefault(u => u.FullName == name);


        private async Task<ChannelUser> AddUserToChannelInternal(Channel channelId, string userName)
        {
            ChannelUser result = null;

            return result;
        }

        public async Task<ChannelUser> AddUserToChannel(string channelName, string userName)
        {
            var channel = await GetChannelByName(channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel not found");
                return null;
            }

            // add user to channel call
            var response = await AddUserToChannelInternal(channel, userName);

            return response;
        }

        public async Task CloseChannel(string channelName)
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



        public async Task<List<ChannelUser>> GetAllUsers(string userPrefix = "") =>
            (await graphService.GetUsers()).Select(u => ToChannelUser(u)).ToList();


        public async Task<List<ChannelUser>> GetChannelUsers(string channelName)
        {
            // 1. getchannels
            // 2. get users in certain channel


            return null;
        }


        public async Task<List<ChannelMessage>> GetMessages(string channelName, DateTime? from)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ChannelMessage>> GetMessages(string channelName)
        {
            throw new NotImplementedException();
        }

        public async Task RemoveUserFromChannel(string channelName, string userName)
        {
            throw new NotImplementedException();
        }

        public async Task SendMessage(string channelName, string message)
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
    }
}