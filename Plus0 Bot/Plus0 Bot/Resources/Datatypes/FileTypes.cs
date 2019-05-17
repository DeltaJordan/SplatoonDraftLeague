using System;
using System.Collections.Generic;
using System.Text;

namespace Plus0_Bot.Resources.Datatypes
{
    public class Setting
    {
        public string token { get; set; }
        public ulong owner { get; set; }
        public List<ulong> log { get; set; }
        public string version { get; set; }
        public List<ulong> banned { get; set; }

    }
    public class Sticker
    {
        public string name { get; set; }
        public string file { get; set; }
        public string description { get; set; }
    }
}
