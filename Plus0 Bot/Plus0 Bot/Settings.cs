using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Plus0_Bot
{
    public class Settings
    {
        [JsonProperty("bot_token")]
        public string BotToken { get; set; }

        [JsonProperty("app_key")]
        public string AppKey { get; set; }

        [JsonProperty("base_id")]
        public string BaseId { get; set; }

        public void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);

            File.WriteAllText(Path.Combine(Globals.AppPath, "Data", "settings.json"), json);
        }
    }
}
