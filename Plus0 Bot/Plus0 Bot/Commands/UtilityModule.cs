using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace Plus0_Bot.Commands
{
    public class UtilityModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping"), 
         Summary("Measures latency, probably inaccurate and is mainly to check the bot's status")]
        public async Task Ping()
        {
            await this.ReplyAsync($"Pong! ({(DateTimeOffset.Now - this.Context.Message.Timestamp).Milliseconds}ms)");
        }
    }
}
