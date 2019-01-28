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
        Task<ChannelUser> AddUserToChannel(string channelName, string userName);
        Task<bool> CreateChannel(string channelName, List<string> channelUsers);
        Task<List<ChannelUser>> GetAllUsers(string userPrefix = "");
        Task<List<ChannelUser>> GetChannelUsers(string channelName);
        Task<List<ChannelMessage>> GetMessages(string channelName, DateTime? from);
        Task<List<ChannelMessage>> GetMessages(string channelName);
        Task RemoveUserFromChannel(string channelName, string userName);
        Task SendMessage(string channelName, string message);
        Task Connect();
        Task CloseChannel(string channelName);

        string Provider{ get; }
    }
}
