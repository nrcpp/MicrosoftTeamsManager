
using System.Runtime.Serialization;

namespace Siemplify.Common.ExternalChannels.DataModel
{
    [DataContract]
    public class ChannelUser
    {
        [DataMember]
        public string UserId { get; set; }
        [DataMember]
        public string FullName { get; set; }
        [DataMember]
        public string Picture { get; set; }
    }
}

