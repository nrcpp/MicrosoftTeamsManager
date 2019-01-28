using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Siemplify.Common.ExternalChannels.DataModel;
//using Siemplify.Server.WebApi;

namespace Siemplify.Common.ExternalChannels
{
    interface IExternalChannelProvider
    {
        ChannelUser AddUserToChannel(string channelName, string userName);
        bool CreateChannel(string channelName, List<string> channelUsers);
        List<ChannelUser> GetAllUsers(string userPrefix = "");
        List<ChannelUser> GetChannelUsers(string channelName);
        List<ChannelMessage> GetMessages(string channelName, DateTime? from);
        List<ChannelMessage> GetMessages(string channelName);
        void RemoveUserFromChannel(string channelName, string userName);
        void SendMessage(string channelName, string message);
        void Connect();
        void CloseChannel(string channelName);

        string Provider{ get; }
    }
}
