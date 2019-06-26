using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using SquidDraftLeague.AirTable;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Set")]
    public class SetModule : ModuleBase<SocketCommandContext>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("pickfor"),
         RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task PickFor(int setNumber, IUser pick)
        {
            Set playerMatch = Matchmaker.Sets[setNumber - 1];

            SdlPlayer sdlPlayer = await AirTableClient.RetrieveSdlPlayer(pick.Id);

            await PickPlayer(playerMatch, sdlPlayer, (SocketTextChannel) this.Context.Channel);
        }

        [Command("pickall"),
         RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task PickAll(int setNumber)
        {
            Set playerMatch = Matchmaker.Sets[setNumber - 1];

            List<SdlPlayer> allDraftPlayers = playerMatch.DraftPlayers.SkipLast(1).ToList();

            foreach (SdlPlayer allDraftPlayer in allDraftPlayers)
            {
                await PickPlayer(playerMatch, allDraftPlayer, (SocketTextChannel) this.Context.Channel);
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

                Set playerMatch = Matchmaker.Sets.FirstOrDefault(e => e.GetPickingTeam().IsCaptain(user.Id));

                if (playerMatch == null || !CommandHelper.SetChannelIds.Contains(this.Context.Channel.Id))
                    return;

                SdlPlayer sdlPlayer = await AirTableClient.RetrieveSdlPlayer(pick.Id);

                await PickPlayer(playerMatch, sdlPlayer, (SocketTextChannel) this.Context.Channel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static async Task PickPlayer(Set playerMatch, SdlPlayer pick, SocketTextChannel context)
        {
            PickPlayerResponse pickPlayerResponse = Matchmaker.PickSetPlayer(pick, playerMatch);

            if (!pickPlayerResponse.Success)
            {
                if (!string.IsNullOrWhiteSpace(pickPlayerResponse.Message))
                    await context.SendMessageAsync(pickPlayerResponse.Message);

                if (pickPlayerResponse.Exception != null)
                    Logger.Error(pickPlayerResponse.Exception);

                return;
            }

            await context.SendMessageAsync($"Added {pick.DiscordId.ToUserMention()}.");

            if (pickPlayerResponse.LastPlayer)
            {
                await context.SendMessageAsync("There is only one player left! Drafting them automatically.");
                await MatchModule.MoveToMatch(context, playerMatch);
            }
            else
            {
                await context.SendMessageAsync($"{playerMatch.GetPickingTeam().Captain.DiscordId.ToUserMention()} it is your turn to pick.", embed: playerMatch.GetEmbedBuilder().Build());
            }
        }
    }
}
