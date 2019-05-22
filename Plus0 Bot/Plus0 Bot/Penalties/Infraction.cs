using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog.Layouts;

namespace Plus0_Bot.Penalties
{
    public class Infraction
    {
        [JsonProperty("penalty")]
        public int Penalty { get; set; }
        [JsonProperty("notes")]
        public string Notes { get; set; }
        [JsonProperty("time_of_offense")]
        public DateTime TimeOfOffense { get; set; }
    }
}
