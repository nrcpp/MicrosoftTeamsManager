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
    interface IExternalChannelProviderAsync
    {
        Task<ChannelUser> AddUserToChannelAsync(string channelName, string userName);
        Task<bool> CreateChannelAsync(string channelName, List<string> channelUsers);
        Task<List<ChannelUser>> GetAllUsersAsync(string userPrefix = "");
        Task<List<ChannelUser>> GetChannelUsersAsync(string channelName);
        Task<List<ChannelMessage>> GetMessagesAsync(string channelName, DateTime? from);
        Task<List<ChannelMessage>> GetMessagesAsync(string channelName);
        Task RemoveUserFromChannelAsync(string channelName, string userName);
        Task SendMessageAsync(string channelName, string message);
        Task ConnectAsync();
        Task CloseChannelAsync(string channelName);

        string Provider{ get; }
    }
}
