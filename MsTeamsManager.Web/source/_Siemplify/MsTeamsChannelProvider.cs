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
    public class MsTeamsChannelProvider : IExternalChannelProvider
    {
        readonly GraphService graphService = new GraphService();
        
        public string CurrentTeamId { get; set; }
        public FormOutput LastResult { get; private set; }

        public MsTeamsChannelProvider()
        {

        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");


        private async Task<FormOutput> WithExceptionHandling(Func<string, FormOutput> f, [CallerMemberName] string callerName = "")
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
                output = f(accessToken);

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
        public string Provider => nameof(MsTeamsChannelProvider);


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
        

        private async Task<bool> CreateChannelInternal(string channelName, string channelDescription)
        {            
            string token = await AuthProvider.Instance.GetUserAccessTokenAsync();

            try
            {
                await graphService.CreateChannel(token, CurrentTeamId, channelName, channelDescription);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return false;
            }

            return true;
        }


        public async Task<bool> CreateChannel(string channelName, List<string> channelUsers)
        {
            if ( !(await CreateChannelInternal(channelName, channelDescription: "")) )
                return false;

            foreach (var user in channelUsers)
                await AddUserToChannel(channelName, user);

            return true;
        }


        public async Task<ChannelUser> AddUserToChannel(string channelName, string userName)
        {
            var result = new ChannelUser()
            {

            };

            return result;
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
            throw new NotImplementedException();
        }

        #endregion
    }
}