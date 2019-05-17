using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

//This is just a command to get an invite to the server if for whatever reason we may not be able to access it
namespace Plus0_Bot.Core.Moderation
{
    public class Backdoor : ModuleBase<SocketCommandContext>
    {
        [Command("backdoor"), Summary("get the server invite")]
        public async Task BackdoorModule(ulong GuildId)
        {
            if(!(Context.User.Id == 301733804949766144))
            {
                await Context.Channel.SendMessageAsync(" :x: You're not a bot moderator SMH");
                return;
            }
            if(Context.Client.Guilds.Where(x => x.Id == GuildId).Count() < 1)
            {
                await Context.Channel.SendMessageAsync(":x: Jordi you dingus, your not in that guild, SMH, Git Gud, here is the ID you Baka: " + GuildId);
                return;
            }

            SocketGuild Guild = Context.Client.Guilds.Where(x => x.Id == GuildId).FirstOrDefault();
            
            var Invites = await Guild.GetInvitesAsync();
                if(Invites.Count < 1)
                {
                    try
                    {
                     await Guild.TextChannels.First().CreateInviteAsync();
                    }
                    catch (Exception ex)
                    {
                    await Context.Channel.SendMessageAsync($":x: Creating an invite for guild {Guild.Name} went Wrong with error ''{ex.Message}'' ");
                    return;
                    }
                }

                EmbedBuilder Embed = new EmbedBuilder();

                Embed.WithAuthor($"Invites for Guild {Guild.Name}: ", Guild.IconUrl);
                Embed.WithColor(40,20,160);

                foreach (var Current in Invites)
                    Embed.AddField("Invite:", $"[Invites]({Current.Url})");

            await Context.Channel.SendMessageAsync("",false, Embed.Build());
            
            
        }
    }
}
