using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace Plus0_Bot.Core.Commands
{
    public class HelloWorld : ModuleBase<SocketCommandContext>
    {
        [Command("Hello"), Alias("Hi"), Summary("says Hello")]
        public async Task Hello()
        {
            await Context.Channel.SendMessageAsync("Hello");
        }
        [Command("lenny"), Alias("Hi"), Summary("says Hello")]
        public async Task lenny()
        {
            await Context.Channel.SendMessageAsync("( ͡° ͜ʖ ͡°)");
        }


    }
}
