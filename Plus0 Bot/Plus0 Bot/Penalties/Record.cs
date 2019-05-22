using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Plus0_Bot.Penalties
{
    public class Record
    {
        [JsonProperty("infractions")]
        public List<Infraction> AllInfractions { get; set; }
        [JsonProperty("comments")]
        public string[] Comments { get; set; }

        public int InfractionsThisMonth()
        {
            return this.AllInfractions.Count(e => e.TimeOfOffense.Month == DateTime.Now.Month);
        }
    }
}
