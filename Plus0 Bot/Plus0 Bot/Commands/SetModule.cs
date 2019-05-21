using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Plus0_Bot.AirTable;
using Plus0_Bot.Queuing;

namespace Plus0_Bot.Commands
{
    public class SetModule : ModuleBase<SocketCommandContext>
    {
        public static readonly ReadOnlyCollection<Set> Sets = new List<Set>
        {
            new Set(1),
            new Set(2),
            new Set(3)
        }.AsReadOnly();

        [Command("pick")]
        public async Task Pick([Remainder,
                                Summary("Person you want to pick.")]
            IUser pick)
        {
            if (!(this.Context.User is IGuildUser user))
                return;

            Set playerMatch = Sets.FirstOrDefault(e => e.AlphaCaptain.DiscordUser.Id == user.Id || e.BravoCaptain.DiscordUser.Id == user.Id);

            if (playerMatch == null)
                return;

            bool isAlphaCaptain = playerMatch.AlphaCaptain.DiscordUser.Id == user.Id;

            if (isAlphaCaptain != playerMatch.AlphaPicking)
            {
                await this.ReplyAsync("It is not your turn to pick!");
                return;
            }

            if (playerMatch.DraftPlayers.Any(e => e.DiscordUser.Id == pick.Id))
            {
                SdlPlayer sdlPick = playerMatch.DraftPlayers.Find(e => e.DiscordUser.Id == pick.Id);
                playerMatch.DraftPlayers.Remove(sdlPick);

                if (isAlphaCaptain)
                {
                    playerMatch.AlphaTeam.Add(sdlPick);

                    await this.ReplyAsync($"Added {pick.Mention} to Team Alpha.");
                    await this.ReplyAsync($"{playerMatch.BravoCaptain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                }
                else
                {
                    playerMatch.BravoTeam.Add(sdlPick);

                    await this.ReplyAsync($"Added {pick.Mention} to Team Bravo.\n");
                    await this.ReplyAsync($"{playerMatch.AlphaCaptain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                }

                playerMatch.AlphaPicking = !isAlphaCaptain;
            }
            else
            {
                await this.ReplyAsync("This player is not in the match.");
            }
        }
    }
}
