using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MSTeamsManager.Models
{
    public class Clone
    {
        public string displayName { get; set; }
        public string description { get; set; }
        public string mailNickName { get; set; }
        public string teamVisibilityType { get; set; }
        public string partsToClone { get; set; } // "apps,members,settings,tabs,channels"
    }
}