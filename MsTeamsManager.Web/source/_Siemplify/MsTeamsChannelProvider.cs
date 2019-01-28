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
        

        public MsTeamsChannelProvider()
        {

        }

        #region Helper methods

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");
        

        private async Task<ActionResult> WithExceptionHandling(Func<string, FormOutput> f, [CallerMemberName] string callerName = "")
        {
            return await WithExceptionHandlingAsync(
                async s => f(s),
                callerName);
        }

        private async Task<ActionResult> WithExceptionHandlingAsync(Func<string, Task<FormOutput>> f, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Configuration settings have to be set
                if (ConfigurationManager.AppSettings["ida:AppId"] == null
                    || ConfigurationManager.AppSettings["ida:AppSecret"] == null)
                {
                    //return RedirectToAction("Index", "Error", new
                    //{
                    //    message = "You need to put your appid and appsecret in Web.config.secrets. See README.md for details."
                    //});
                    return null;
                }

                // Get an access token.
                string accessToken = await AuthProvider.Instance.GetUserAccessTokenAsync();
                graphService.accessToken = accessToken;
                FormOutput output = await f(accessToken);

                output.Action = callerName.Replace("Form", "Action");

                output.UserUpn = await graphService.GetMyId(accessToken); // todo: cache

                if (output.ShowTeamDropdown)
                    output.Teams = (await graphService.GetMyTeams(accessToken)).ToArray();
                if (output.ShowGroupDropdown)
                    output.Groups = (await graphService.GetMyGroups(accessToken)).ToArray();

                //results.Items = await graphService.GetMyTeams(accessToken, Convert.ToString(Resource.Prop_ID));
                //return View("Graph", output);                
            }
            catch (Exception )
            {
                //if (e.Message == Resource.Error_AuthChallengeNeeded) return new EmptyResult();
                //return RedirectToAction("Index", "Error", new { message = Resource.Error_Message + Request.RawUrl + ": " + e.Message });
            }

            return null;
        }

        #endregion


        #region IExternalChannelProvider implementation

        // Provider name
        public string Provider => nameof(MsTeamsChannelProvider);


        // See also: App_Start\Startup.Auth.cs
        public void Connect()
        {
            var result = WithExceptionHandling(
                token => new FormOutput()
            );

            Log(result.Result.ToString());
        }


        private bool CreateChannelInternal(string channelName, string channelDescription)
        {            
            string token = AuthProvider.Instance.GetUserAccessTokenAsync().Result;

            try
            {
                graphService.CreateChannel(token, CurrentTeamId, channelName, channelDescription).RunSynchronously();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return false;
            }

            return true;

#if false
            await WithExceptionHandlingAsync(
                async token =>
                {
                    await graphService.CreateChannel(token,
                        data.SelectedTeam, data.NameInput, data.DescriptionInput);
                    var channels = (await graphService.GetChannels(token, data.SelectedTeam)).ToArray();
                    return new FormOutput()
                    {
                        Channels = channels,
                        ShowChannelOutput = true
                    };
                }
                );
#endif
        }

        public bool CreateChannel(string channelName, List<string> channelUsers)
        {
            if (!CreateChannelInternal(channelName, channelDescription: ""))
                return false;

            foreach (var user in channelUsers)
                AddUserToChannel(channelName, user);

            return true;
        }


        public ChannelUser AddUserToChannel(string channelName, string userName)
        {
            var result = new ChannelUser()
            {

            };

            return result;
        }

        public void CloseChannel(string channelName)
        {
            
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

#endregion
    }
}