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

        [Command("pickfor"),
         RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task PickFor(int setNumber, IUser pick)
        {
            Set playerMatch = Sets[setNumber - 1];

            SdlPlayer sdlPick = playerMatch.DraftPlayers.Find(e => e.DiscordUser.Id == pick.Id);
            playerMatch.DraftPlayers.Remove(sdlPick);

            if (playerMatch.AlphaPicking)
            {
                playerMatch.AlphaTeam.AddPlayer(sdlPick);

                await this.ReplyAsync($"Added {pick.Mention} to Team Alpha.");
                await this.ReplyAsync($"{playerMatch.BravoTeam.Captain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
            }
            else
            {
                playerMatch.BravoTeam.AddPlayer(sdlPick);

                await this.ReplyAsync($"Added {pick.Mention} to Team Bravo.\n");
                await this.ReplyAsync($"{playerMatch.AlphaTeam.Captain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
            }

            if (playerMatch.DraftPlayers.Count == 1)
            {
                await this.ReplyAsync("There is only one player left! Drafting them automatically.");

                if (playerMatch.AlphaPicking)
                {
                    playerMatch.BravoTeam.AddPlayer(playerMatch.DraftPlayers.First());
                }
                else
                {
                    playerMatch.AlphaTeam.AddPlayer(playerMatch.DraftPlayers.First());
                }

                playerMatch.DraftPlayers.Clear();

                await this.ReplyAsync(embed: playerMatch.GetEmbedBuilder().Build());
            }

            playerMatch.AlphaPicking = !playerMatch.AlphaPicking;
        }

        [Command("pick")]
        public async Task Pick(
            [Remainder,
             Summary("Person you want to pick.")]
             IUser pick)
        {
            try
            {
                if (!(this.Context.User is IGuildUser user))
                    return;

                Set playerMatch = Sets.FirstOrDefault(e => e.AlphaTeam.IsCaptain(user) || e.BravoTeam.IsCaptain(user));

                if (playerMatch == null)
                    return;

                bool isAlphaCaptain = playerMatch.AlphaTeam.IsCaptain(user);

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
                        playerMatch.AlphaTeam.AddPlayer(sdlPick);

                        await this.ReplyAsync($"Added {pick.Mention} to Team Alpha.");
                        await this.ReplyAsync($"{playerMatch.BravoTeam.Captain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                    }
                    else
                    {
                        playerMatch.BravoTeam.AddPlayer(sdlPick);

                        await this.ReplyAsync($"Added {pick.Mention} to Team Bravo.\n");
                        await this.ReplyAsync($"{playerMatch.AlphaTeam.Captain.DiscordUser.Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                    }

                    if (playerMatch.DraftPlayers.Count == 1)
                    {
                        await this.ReplyAsync("There is only one player left! Drafting them automatically.");

                        if (isAlphaCaptain)
                        {
                            playerMatch.BravoTeam.AddPlayer(playerMatch.DraftPlayers.First());
                        }
                        else
                        {
                            playerMatch.AlphaTeam.AddPlayer(playerMatch.DraftPlayers.First());
                        }

                        playerMatch.DraftPlayers.Clear();
                    }

                    playerMatch.AlphaPicking = !isAlphaCaptain;
                }
                else
                {
                    await this.ReplyAsync("This player is not in the match.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
