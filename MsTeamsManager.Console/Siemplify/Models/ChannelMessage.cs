
using System;
using System.Runtime.Serialization;

namespace Siemplify.Common.ExternalChannels.DataModel
{
    [DataContract]
    public class ChannelMessage
    {
        [DataMember]
        public DateTime Time { get; set; }
        [DataMember]
        public string User { get; set; }
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public string ChannelId { get; set; }
        [DataMember]
        public bool IsStarred { get; set; }
    }
}
