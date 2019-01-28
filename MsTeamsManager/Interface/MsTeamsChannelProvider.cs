using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Siemplify.Common.ExternalChannels.DataModel;


namespace Siemplify.Common.ExternalChannels
{
    public class MsTeamsChannelProvider : IExternalChannelProvider
    {
        public string Provider => nameof(MsTeamsChannelProvider);

        public static void Log(string msg, [CallerMemberName] string caller = null) =>
            Console.WriteLine($"[{caller}]: {msg}");


        public bool CreateChannel(string channelName, List<string> channelUsers)
        {
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

        public void Connect()
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
    }
}