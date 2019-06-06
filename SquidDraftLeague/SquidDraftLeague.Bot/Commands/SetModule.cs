using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Bot.Queuing;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Set")]
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

            await PickPlayer(playerMatch, pick, (ITextChannel) this.Context.Channel);
        }

        [Command("pickall"),
         RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task PickAll(int setNumber)
        {
            Set playerMatch = Sets[setNumber - 1];

            List<SdlPlayer> allDraftPlayers = playerMatch.DraftPlayers.SkipLast(1).ToList();

            foreach (SdlPlayer allDraftPlayer in allDraftPlayers)
            {
                await PickPlayer(playerMatch, allDraftPlayer.DiscordId.GetGuildUser(this.Context), (ITextChannel)this.Context.Channel);
            }
        }

        [Command("pick"),
         Summary("Picks a person to join the team of the set you are in.")]
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

                await PickPlayer(playerMatch, pick, (ITextChannel)this.Context.Channel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static async Task PickPlayer(Set playerMatch, IUser pick, ITextChannel context)
        {
            if (playerMatch.DraftPlayers.Any(e => e.DiscordId.GetGuildUser(null).Id == pick.Id))
            {
                playerMatch.ResetTimeout();

                SdlPlayer sdlPick = playerMatch.DraftPlayers.Find(e => e.DiscordId.GetGuildUser(null).Id == pick.Id);
                playerMatch.DraftPlayers.Remove(sdlPick);

                if (playerMatch.AlphaPicking)
                {
                    playerMatch.AlphaTeam.AddPlayer(sdlPick);

                    await context.SendMessageAsync($"Added {pick.Mention} to Team Alpha.");
                    await context.SendMessageAsync($"{playerMatch.BravoTeam.Captain.DiscordId.GetGuildUser(null).Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                }
                else
                {
                    playerMatch.BravoTeam.AddPlayer(sdlPick);

                    await context.SendMessageAsync($"Added {pick.Mention} to Team Bravo.\n");
                    await context.SendMessageAsync($"{playerMatch.AlphaTeam.Captain.DiscordId.GetGuildUser(null).Mention} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
                }

                if (playerMatch.DraftPlayers.Count == 1)
                {
                    await context.SendMessageAsync("There is only one player left! Drafting them automatically.");

                    if (playerMatch.AlphaPicking)
                    {
                        playerMatch.BravoTeam.AddPlayer(playerMatch.DraftPlayers.First());
                    }
                    else
                    {
                        playerMatch.AlphaTeam.AddPlayer(playerMatch.DraftPlayers.First());
                    }

                    playerMatch.DraftPlayers.Clear();

                    await MatchModule.MoveToMatch(context, playerMatch);
                }

                playerMatch.AlphaPicking = !playerMatch.AlphaPicking;

                playerMatch.ResetTimeout();
            }
            else
            {
                await context.SendMessageAsync("This player is not available to be drafted.");
            }
        }
    }
}
