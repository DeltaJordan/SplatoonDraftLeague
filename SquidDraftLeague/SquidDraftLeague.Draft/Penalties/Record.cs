using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SquidDraftLeague.Draft.Penalties
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
