using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Plus0_Bot.Core.Data;
using Plus0_Bot.Resources.Datatypes;

//this file may be deleted, it was jsut part of the tutorial project I followed to learn the ins and outs of Disocrd.net
namespace Plus0_Bot.Core.Commands
{
    public class Stickers:ModuleBase<SocketCommandContext>
    {
        [Command("getsticker"), Summary("Gets a random sticker from the xml file")]
        public async Task GetSticker()
        {
            Sticker Generated = Data.Data.GetSticker();
            if(Generated == null)
            {
                await Context.Channel.SendMessageAsync(":x: The file was not found :frowning:");
            }
            EmbedBuilder Embed = new EmbedBuilder();

            Embed.WithAuthor($"Here is your Sticker - {Generated.name}");
            Embed.WithImageUrl(Generated.file);
            Embed.WithFooter(Generated.description);

            await Context.Channel.SendMessageAsync("", false, Embed.Build());
        }
    }
}
