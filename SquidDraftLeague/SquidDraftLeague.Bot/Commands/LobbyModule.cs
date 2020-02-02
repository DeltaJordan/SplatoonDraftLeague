using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using NLog;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.Language.Resources;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [RequireChannel(572536965833162753)]
    public class LobbyModule
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("fill"),
        RequireRole("Developer")]
        public async Task DebugPropagate(CommandContext ctx)
        {
            SdlPlayer sdlPlayer = await MySqlClient.RetrieveSdlPlayer(ctx.User.Id);

            await this.JoinLobby(ctx, sdlPlayer, true);
        }

        [Command("join"),
         Description("Join an existing lobby near your power level, or creates a new lobby if none exist.")]
        public async Task Join(CommandContext ctx, int lobbyNum = 0)
        {
            if (!(ctx.User is DiscordMember))
                return;

            DiscordMember player = (DiscordMember)ctx.User;

            LobbyEligibilityResponse lobbyEligibility = Matchmaker.LobbyEligibility(player.Id);

            if (!lobbyEligibility.Success)
            {
                await ctx.RespondAsync(lobbyEligibility.Message);
                return;
            }

            SdlPlayer sdlPlayer;

            try
            {
                sdlPlayer = await MySqlClient.RetrieveSdlPlayer(player.Id);
            }
            catch (SdlMySqlException exception)
            {
                Logger.Error(exception);

                switch (exception.Type)
                {
                    case SdlMySqlException.ExceptionType.ZeroUpdates:
                        await (await ctx.Guild.GetMemberAsync((await Program.Client.GetCurrentApplicationAsync()).Owner
                            .Id)).SendMessageAsync(exception.Message);

                    await ctx.RespondAsync("Cannot find your record in the database. " +
                                          "Most likely either you have not registered or are not registered correctly.");
                    break;
                    case SdlMySqlException.ExceptionType.DuplicateEntry:
                        await (await ctx.Guild.GetMemberAsync((await Program.Client.GetCurrentApplicationAsync()).Owner
                            .Id)).SendMessageAsync(exception.Message);

                        await ctx.RespondAsync("There was an error retrieving your player record.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return;
            }

            int? num = null;

            if (lobbyNum != 0)
            {
                num = lobbyNum;
            }

            await this.JoinLobby(ctx, sdlPlayer, lobbyNumber: num);
        }

        [Command("tTimeout"), RequireOwner]
        public async Task TestTimeout(CommandContext ctx)
        {
            Matchmaker.Lobbies[0].AddPlayer(await MySqlClient.RetrieveSdlPlayer(ctx.User.Id), true);

            MatchedLobby_DeltaUpdated(Matchmaker.Lobbies[0], true);
        }

        private async Task JoinLobby(CommandContext ctx, SdlPlayer sdlPlayer, bool debugFill = false, int? lobbyNumber = null)
        {
            try
            {
                Lobby matchedLobby;

                if (lobbyNumber != null)
                {
                    LobbySelectResponse lobbySelectResponse = Matchmaker.SelectLobbyByNumber(sdlPlayer, lobbyNumber.Value);

                    if (!lobbySelectResponse.Success)
                    {
                        if (lobbySelectResponse.Exception != null)
                            Logger.Error(lobbySelectResponse.Exception);

                        if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                            await ctx.RespondAsync(lobbySelectResponse.Message);

                        return;
                    }

                    matchedLobby = lobbySelectResponse.Result;

                    if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                        await ctx.RespondAsync(lobbySelectResponse.Message);
                }
                else
                {
                    LobbySelectResponse lobbySelectResponse = Matchmaker.FindLobby(sdlPlayer);

                    if (!lobbySelectResponse.Success)
                    {
                        if (lobbySelectResponse.Exception != null)
                            Logger.Error(lobbySelectResponse.Exception);

                        if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                            await ctx.RespondAsync(lobbySelectResponse.Message);

                        return;
                    }

                    if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                        await ctx.RespondAsync(lobbySelectResponse.Message);

                    matchedLobby = lobbySelectResponse.Result;
                }

                matchedLobby.AddPlayer(sdlPlayer);

                if (debugFill)
                {
                    foreach (SdlPlayer nextPlayer in (await MySqlClient.RetrieveAllSdlPlayers()).Where(e => e != sdlPlayer).Take(7))
                    {
                        matchedLobby.AddPlayer(nextPlayer, true);
                    }
                }

                if (matchedLobby.IsFull)
                {
                    MoveToSetResponse moveToSetResponse = Matchmaker.MoveLobbyToSet(matchedLobby);

                    if (!moveToSetResponse.Success)
                    {
                        if (moveToSetResponse.Exception != null)
                            Logger.Error(moveToSetResponse.Exception);

                        if (moveToSetResponse.Message != null)
                            await ctx.RespondAsync(moveToSetResponse.Message,
                                embed: matchedLobby.GetEmbedBuilder().Build());

                        return;
                    }

                    if (!string.IsNullOrEmpty(moveToSetResponse.Message))
                        await ctx.RespondAsync(moveToSetResponse.Message);

                    Set newSet = moveToSetResponse.Result;
                    newSet.Closed += this.NewMatch_Closed;

                    DiscordRole setRole =
                        ctx.Guild.Roles.First(e => e.Name == $"In Set ({newSet.SetNumber})");

                    foreach (SdlPlayer setPlayer in newSet.AllPlayers)
                    {
                        DiscordMember member = await ctx.Guild.GetMemberAsync(setPlayer.DiscordId);
                        await member.GrantRoleAsync(setRole);
                    }

                    DiscordChannel setChannel = await CommandHelper.ChannelFromSet(newSet.SetNumber);

                    await ctx.RespondAsync($"Lobby filled! Please move to {setChannel.Mention}.");

                    await ctx.Guild.UpdateRoleAsync(setRole, mentionable: true);
                    DiscordMessage lastMessage = await setChannel.SendMessageAsync(
                        $"{setRole.Mention} Welcome to set #{newSet.SetNumber}! To begin, " +
                        $"{(await ctx.Guild.GetMemberAsync(newSet.BravoTeam.Captain.DiscordId)).Mention} will have " +
                        $"one minute to pick a player using `%pick [player]`.",
                        embed: newSet.GetEmbedBuilder().Build());
                    await ctx.Guild.UpdateRoleAsync(setRole, mentionable: false);

                    newSet.DraftTimeout += NewSet_DraftTimeout;
                    newSet.ResetTimeout();
                }
                else
                {
                    string message =
                        $"{sdlPlayer.DiscordId.ToUserMention()} has been added to " +
                        $"Lobby #{matchedLobby.LobbyNumber}. {8 - matchedLobby.Players.Count} players needed to begin.";

                    DiscordRole classOneRole = ctx.Guild.GetRole(600770643075661824);
                    DiscordRole classTwoRole = ctx.Guild.GetRole(600770814521901076);
                    DiscordRole classThreeRole = ctx.Guild.GetRole(600770862307606542);
                    DiscordRole classFourRole = ctx.Guild.GetRole(600770905282576406);

                    DiscordRole[] notifRoles = {classOneRole, classTwoRole, classThreeRole, classFourRole};

                    if (matchedLobby.Players.Count == 1)
                    {
                        matchedLobby.DeltaUpdated += MatchedLobby_DeltaUpdated;

                        message = $"{notifRoles[(int)matchedLobby.Class - 1].Mention} " +
                                  $"{((int)matchedLobby.Class - 2 > 0 ? notifRoles[(int)matchedLobby.Class - 2].Mention + " " : "")}" +
                                  $"A new lobby has been started! {message}";
                    }

                    DiscordEmbedBuilder builder = matchedLobby.GetEmbedBuilder();

                    await ctx.RespondAsync(message, false, builder.Build());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Logger.Error(e);
                throw;
            }
        }

        private static async void MatchedLobby_DeltaUpdated(object sender, bool closed)
        {
            if (sender == null || !(sender is Lobby lobby))
            {
                Logger.Warn("Abnormal behavoir detected in Lobby's DeltaUpdated event handler: sender is either not a lobby or is null.");
                return;
            }

            // Temporary fix for Lobby class clearing this too early.
            List<SdlPlayer> players = lobby.Players.Select(x => x).ToList();

            if (closed)
            {
                try
                {
                    await (await Program.Client.GetChannelAsync(572536965833162753)).SendMessageAsync(
                        $"{string.Join(" ", players.Select(async f => (await Program.Client.GetUserAsync(f.DiscordId)).Mention))}\n" +
                        $"Closing the lobby because not enough players have joined the battle. Please try again by using %join.");

                    string activityDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Player Activity")).FullName;

                    foreach (SdlPlayer lobbyPlayer in players)
                    {
                        PlayerActivity playerActivity;
                        string playerFile = Path.Combine(activityDirectory, $"{lobbyPlayer.DiscordId}.json");

                        if (File.Exists(playerFile))
                        {
                            playerActivity =
                                JsonConvert.DeserializeObject<PlayerActivity>(await File.ReadAllTextAsync(playerFile));
                        }
                        else
                        {
                            playerActivity = new PlayerActivity
                            {
                                PlayedSets = new List<DateTime>(),
                                Timeouts = new List<DateTime>()
                            };
                        }

                        if (!playerActivity.Timeouts.Any() || playerActivity.Timeouts.All(e => e.Date != DateTime.UtcNow.Date))
                        {
                            playerActivity.Timeouts.Add(DateTime.UtcNow);
                        }

                        await File.WriteAllTextAsync(playerFile, JsonConvert.SerializeObject(playerActivity));
                    }

                    Logger.Info($"Recorded {players.Count} players timed out in lobby #{lobby.LobbyNumber}.");
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                return;
            }

            string message =
                $"{(lobby.CurrentDelta - 75) / 25 * 5} minutes have passed for lobby #{lobby.LobbyNumber}. The threshold has been increased by 25 to {lobby.CurrentDelta}.";

            DiscordEmbedBuilder builder = lobby.GetEmbedBuilder();

            await (await Program.Client.GetChannelAsync(572536965833162753)).SendMessageAsync(message, false, builder.Build());
        }

        private static async void NewSet_DraftTimeout(object sender, EventArgs e)
        {
            if (sender == null || !(sender is Set set))
                return;

            DiscordChannel setChannel = await CommandHelper.ChannelFromSet(set.SetNumber);

            DiscordMessage message = await setChannel.SendMessageAsync("Choosing team member due to timeout.");

            if (set.DraftPlayers.Any())
                await SetModule.PickPlayer(set, set.DraftPlayers[Globals.Random.Next(0, set.DraftPlayers.Count - 1)], setChannel);
        }

        private async void NewMatch_Closed(object sender, EventArgs eventArgs)
        {
            if (sender == null || !(sender is Set set))
                return;

            set.Closed -= this.NewMatch_Closed;

            if (!Matchmaker.Lobbies.Any(l => l.InStandby)) return;

            Lobby matchedLobby = Matchmaker.Lobbies.First(l => l.InStandby);
            set.MoveLobbyToSet(matchedLobby);
            matchedLobby.Close();

            DiscordChannel setChannel = await CommandHelper.ChannelFromSet(set.SetNumber);
            DiscordGuild sdlGuild = setChannel.Guild;

            DiscordRole setRole =
                sdlGuild.Roles.FirstOrDefault(e => e.Name == $"In Set ({set.SetNumber})");
            DiscordRole devRole = sdlGuild.Roles.First(e => e.Name == "Developer");

            if (setRole == null)
            {
                await setChannel.SendMessageAsync(
                    $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({set.SetNumber})\".");
                return;
            }

            foreach (SdlPlayer setPlayer in set.AllPlayers)
            {
                await (await sdlGuild.GetMemberAsync(setPlayer.DiscordId)).GrantRoleAsync(setRole);
            }

            await setChannel.SendMessageAsync(
                $"Welcome to set #{set.SetNumber}! To begin, {(await sdlGuild.GetMemberAsync(set.BravoTeam.Captain.DiscordId)).Mention} will have two minutes to pick a player using `%pick [player]`.",
                embed: set.GetEmbedBuilder().Build());

            set.DraftTimeout += this.NewMatch_Closed;
        }
    }
}
