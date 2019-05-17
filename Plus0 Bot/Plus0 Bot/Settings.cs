using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Plus0_Bot
{
    public class Settings
    {
        [JsonProperty("bot_token")]
        public string BotToken { get; set; }
    }
}
