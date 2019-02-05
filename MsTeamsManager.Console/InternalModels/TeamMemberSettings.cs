using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MSTeamsManager.Models
{
    public class TeamMemberSettings
    {
        public Boolean allowCreateUpdateChannels { get; set; }
        public Boolean allowDeleteChannels { get; set; }
        public Boolean allowAddRemoveApps { get; set; }
        public Boolean allowCreateUpdateRemoveTabs { get; set; }
        public Boolean allowCreateUpdateRemoveConnectors { get; set; }
    }
}