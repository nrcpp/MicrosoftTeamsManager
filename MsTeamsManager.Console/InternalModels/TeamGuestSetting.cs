using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MSTeamsManager.Models
{
    public class TeamGuestSettings
    {
        public bool allowCreateUpdateChannels { get; set; }
        public bool allowDeleteChannels { get; set; }
    }
}