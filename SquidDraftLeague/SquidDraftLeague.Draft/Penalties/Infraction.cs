using System;
using Newtonsoft.Json;

namespace SquidDraftLeague.Draft.Penalties
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
