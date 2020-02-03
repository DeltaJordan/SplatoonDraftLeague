using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using NLog;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.MySQL;

namespace SquidDraftLeague.Bot.Commands
{
    public class SetModule
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("pickfor")]
        [RequirePermissions(Permissions.ManageGuild)]
        public async Task PickFor(CommandContext ctx, int setNumber, DiscordMember pick)
        {
            Set playerMatch = Matchmaker.Sets[setNumber - 1];

            SdlPlayer sdlPlayer = await MySqlClient.RetrieveSdlPlayer(pick.Id);

            await PickPlayer(playerMatch, sdlPlayer, ctx.Channel);
        }

        [Command("pickall")]
        [RequirePermissions(Permissions.ManageGuild)]
        public async Task PickAll(CommandContext ctx, int setNumber)
        {
            Set playerMatch = Matchmaker.Sets[setNumber - 1];

            List<SdlPlayer> allDraftPlayers = playerMatch.DraftPlayers.SkipLast(1).ToList();

            foreach (SdlPlayer allDraftPlayer in allDraftPlayers)
            {
                await PickPlayer(playerMatch, allDraftPlayer, ctx.Channel);
            }
        }

        [Command("pick")]
        [Description("Picks a person to join the team of the set you are in.")]
        public async Task Pick(CommandContext ctx,
            [RemainingText, Description("Person you want to pick.")]
            DiscordMember pick)
        {
            try
            {
                Set playerMatch = Matchmaker.Sets.FirstOrDefault(e => e.GetPickingTeam().IsCaptain(ctx.Message.Author.Id));

                Logger.Warn(string.Join(",", Matchmaker.Sets[0].BravoTeam.Players.Select(x => x.Nickname)));

                if (playerMatch == null || !CommandHelper.SetChannelIds.Contains(ctx.Channel.Id))
                    return;

                SdlPlayer sdlPlayer = await MySqlClient.RetrieveSdlPlayer(pick.Id);

                await PickPlayer(playerMatch, sdlPlayer, ctx.Channel);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }

        public static async Task PickPlayer(Set playerMatch, SdlPlayer pick, DiscordChannel context)
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
