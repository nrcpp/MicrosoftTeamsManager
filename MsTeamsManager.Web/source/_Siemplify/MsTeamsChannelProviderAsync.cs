using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
        
        public string CurrentTeamId { get; set; }
        public FormOutput LastResult { get; private set; }

        public MsTeamsChannelProviderAsync()
        {

        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");


        private async Task<FormOutput> WithExceptionHandling(Func<string, FormOutput> f, [CallerMemberName] string callerName = "")
        {
            return await WithExceptionHandlingAsync(
                async s => f(s),
                callerName);
        }

        private async Task<FormOutput> WithExceptionHandlingAsync(Func<string, Task<FormOutput>> f, [CallerMemberName] string callerName = "")
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
                output = await f(accessToken);

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

            CurrentTeamId = LastResult.Teams?.FirstOrDefault()?.id;     // select first team on connection
        }
        

        private async Task<FormOutput> CreateChannelInternal(string channelName, string channelDescription)
        {
            LastResult = await WithExceptionHandlingAsync(
                async token =>
                {
                    await graphService.CreateChannel(token,
                        CurrentTeamId, channelName, channelDescription);
                    var channels = (await graphService.GetChannels(token, CurrentTeamId)).ToArray();
                    return new FormOutput()
                    {
                        Channels = channels,
                        ShowChannelOutput = true
                    };
                }
            );

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
                result &= (await AddUserToChannelInternal(channel.id, channelName, user)) != null;

            return result;
        }

        ChannelUser FromResponseUser(User user)
        {
            return new ChannelUser()
            {
                UserId = user.id,

                // TODO: add fields to User
                FullName = "<TODO fullname>", // user.fullname
                Picture = "<TODO picture>",
            };
        }

        private async Task<ChannelUser> AddUserToChannelInternal(string id, string channelName, string userName)
        {
            ChannelUser result = null;

            return result;
        }


        private async Task<Channel []> GetChannels()
        {            
            var response = await WithExceptionHandlingAsync(
                async token =>
                {
                    var channels = (await graphService.GetChannels(token, CurrentTeamId)).ToArray();
                    return new FormOutput()
                    {
                        Channels = channels,
                        ShowChannelOutput = true
                    };
                }
            );

            return response.Channels;
        }

        private async Task<Channel> GetChannelByName(string name) =>        
            (await GetChannels())?.FirstOrDefault(ch => ch.displayName == name);

        public async Task<ChannelUser> AddUserToChannel(string channelName, string userName)
        {
            var channel = await GetChannelByName(channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel not found");
                return null;
            }

            // add user to channel call
            var response = await AddUserToChannelInternal(channel.id, channelName, userName);

            response.
        }

        public async Task CloseChannel(string channelName)
        {
            
        }



        public async Task<List<ChannelUser>> GetAllUsers(string userPrefix = "")
        {
            throw new NotImplementedException();
        }

        public async Task<List<ChannelUser>> GetChannelUsers(string channelName)
        {
            throw new NotImplementedException();
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
                throw new ArgumentException("No channel found - " + channelName);
                
            LastResult = await WithExceptionHandlingAsync(
                async token =>
                {
                    await graphService.PostMessage(token, CurrentTeamId, channelId, message);
                    return new FormOutput()
                    {
                        SuccessMessage = "Done",
                    };
                }
            );
        }

        #endregion
    }
}