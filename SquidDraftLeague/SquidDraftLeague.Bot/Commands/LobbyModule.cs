using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using NLog;
using SquidDraftLeague.AirTable;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.Language.Resources;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Lobby"), CheckPenalty, Group, RequireChannel(572536965833162753)]
    public class LobbyModule : InteractiveBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("fill"),
        RequireRole("Developer")]
        public async Task DebugPropagate()
        {
            SdlPlayer sdlPlayer = await AirTableClient.RetrieveSdlPlayer(this.Context.User.Id);

            await this.JoinLobby(sdlPlayer, true);
        }

        [Command("join"),
         Summary("Join an existing lobby near your power level, or creates a new lobby if none exist.")]
        public async Task Join(int? lobbyNum = null)
        {
            if (!(this.Context.User is IGuildUser))
                return;

            IGuildUser player = (IGuildUser)this.Context.User;

            LobbyEligibilityResponse lobbyEligibility = Matchmaker.LobbyEligibility(player.Id);

            if (!lobbyEligibility.Success)
            {
                await this.ReplyAsync(lobbyEligibility.Message);
                return;
            }

            SdlPlayer sdlPlayer;

            try
            {
                sdlPlayer = await AirTableClient.RetrieveSdlPlayer(player.Id);
            }
            catch (SdlAirTableException exception)
            {
                Logger.Error(exception);

                switch (exception.ErrorType)
                {
                    case SdlAirTableException.AirtableErrorType.NotFound:
                        await (await (await Program.Client.GetApplicationInfoAsync()).Owner.GetOrCreateDMChannelAsync())
                            .SendMessageAsync(exception.Message);

                        await this.ReplyAsync("Cannot find your record in the database. " +
                                              "Most likely either you have not registered or are not registered correctly.");
                        break;
                    case SdlAirTableException.AirtableErrorType.UnexpectedDuplicate:
                    case SdlAirTableException.AirtableErrorType.CommunicationError:
                    case SdlAirTableException.AirtableErrorType.Generic:
                        await (await (await Program.Client.GetApplicationInfoAsync()).Owner.GetOrCreateDMChannelAsync())
                            .SendMessageAsync(exception.Message);

                        await this.ReplyAsync("There was an error retrieving your player record.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return;
            }

            await this.JoinLobby(sdlPlayer, lobbyNumber: lobbyNum);
        }

        private async Task JoinLobby(SdlPlayer sdlPlayer, bool debugFill = false, int? lobbyNumber = null)
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
                            await this.ReplyAsync(lobbySelectResponse.Message);

                        return;
                    }

                    matchedLobby = lobbySelectResponse.Result;

                    if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                        await this.ReplyAsync(lobbySelectResponse.Message);
                }
                else
                {
                    LobbySelectResponse lobbySelectResponse = Matchmaker.FindLobby(sdlPlayer);

                    if (!lobbySelectResponse.Success)
                    {
                        if (lobbySelectResponse.Exception != null)
                            Logger.Error(lobbySelectResponse.Exception);

                        if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                            await this.ReplyAsync(lobbySelectResponse.Message);

                        return;
                    }

                    if (!string.IsNullOrEmpty(lobbySelectResponse.Message))
                        await this.ReplyAsync(lobbySelectResponse.Message);

                    matchedLobby = lobbySelectResponse.Result;
                }

                matchedLobby.AddPlayer(sdlPlayer);

                if (debugFill)
                {
                    foreach (SdlPlayer nextPlayer in (await AirTableClient.RetrieveAllSdlPlayers()).Where(e => e != sdlPlayer).Take(7))
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
                            await this.ReplyAsync(moveToSetResponse.Message,
                                embed: matchedLobby.GetEmbedBuilder().Build());

                        return;
                    }

                    if (!string.IsNullOrEmpty(moveToSetResponse.Message))
                        await this.ReplyAsync(moveToSetResponse.Message);

                    Set newSet = moveToSetResponse.Result;
                    newSet.Closed += this.NewMatch_Closed;

                    SocketRole setRole =
                        this.Context.Guild.Roles.First(e => e.Name == $"In Set ({newSet.SetNumber})");

                    foreach (SdlPlayer setPlayer in newSet.AllPlayers)
                    {
                        await this.Context.Guild.GetUser(setPlayer.DiscordId).AddRoleAsync(setRole);
                    }

                    SocketTextChannel setChannel = CommandHelper.ChannelFromSet(newSet.SetNumber);

                    await this.ReplyAsync($"Lobby filled! Please move to {setChannel.Mention}.");

                    await setRole.ModifyAsync(e => e.Mentionable = true);
                    RestUserMessage lastMessage = await setChannel.SendMessageAsync(
                        $"{setRole.Mention} Welcome to set #{newSet.SetNumber}! To begin, " +
                        $"{this.Context.Guild.GetUser(newSet.BravoTeam.Captain.DiscordId).Mention} will have " +
                        $"one minute to pick a player using `%pick [player]`.",
                        embed: newSet.GetEmbedBuilder().Build());
                    await setRole.ModifyAsync(e => e.Mentionable = false);

                    newSet.DraftTimeout += this.NewSet_DraftTimeout;
                    newSet.ResetTimeout();
                }
                else
                {
                    string message =
                        $"{sdlPlayer.DiscordId.ToUserMention()} has been added to " +
                        $"Lobby #{matchedLobby.LobbyNumber}. {8 - matchedLobby.Players.Count} players needed to begin.";

                    IRole classOneRole = this.Context.Guild.GetRole(600770643075661824);
                    IRole classTwoRole = this.Context.Guild.GetRole(600770814521901076);
                    IRole classThreeRole = this.Context.Guild.GetRole(600770862307606542);
                    IRole classFourRole = this.Context.Guild.GetRole(600770905282576406);

                    IRole[] notifRoles = {classOneRole, classTwoRole, classThreeRole, classFourRole};

                    if (matchedLobby.Players.Count == 1)
                    {
                        matchedLobby.DeltaUpdated += this.MatchedLobby_DeltaUpdated;

                        message = $"{notifRoles[(int)matchedLobby.Class - 1].Mention} " +
                                  $"{((int)matchedLobby.Class - 2 > 0 ? notifRoles[(int)matchedLobby.Class - 2].Mention + " " : "")}" +
                                  $"A new lobby has been started! {message}";
                    }

                    EmbedBuilder builder = matchedLobby.GetEmbedBuilder();

                    await this.ReplyAsync(message, false, builder.Build());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Logger.Error(e);
                throw;
            }
        }

        private async void MatchedLobby_DeltaUpdated(object sender, bool closed)
        {
            if (sender == null || !(sender is Lobby lobby))
                return;

            if (closed)
            {
                await this.Context.Channel.SendMessageAsync(
                    $"{string.Join(" ", lobby.Players.Select(f => Program.Client.GetUser(f.DiscordId).Mention))}\n" +
                    $"Closing the lobby because not enough players have joined the battle. Please try again by using %join.");

                return;
            }

            string message =
                $"{(lobby.CurrentDelta - 75) / 25 * 5} minutes have passed for lobby #{lobby.LobbyNumber}. The threshold has been increased by 25 to {lobby.CurrentDelta}.";

            EmbedBuilder builder = lobby.GetEmbedBuilder();

            await this.Context.Channel.SendMessageAsync(message, false, builder.Build());
        }

        private async void NewSet_DraftTimeout(object sender, EventArgs e)
        {
            if (sender == null || !(sender is Set set))
                return;

            SocketTextChannel setChannel = CommandHelper.ChannelFromSet(set.SetNumber);

            await setChannel.SendMessageAsync("Choosing team member due to timeout.");

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

            SocketTextChannel setChannel = CommandHelper.ChannelFromSet(set.SetNumber);
            SocketGuild sdlGuild = setChannel.Guild;

            SocketRole setRole =
                sdlGuild.Roles.FirstOrDefault(e => e.Name == $"In Set ({set.SetNumber})");
            SocketRole devRole = sdlGuild.Roles.First(e => e.Name == "Developer");

            if (setRole == null)
            {
                await setChannel.SendMessageAsync(
                    $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({set.SetNumber})\".");
                return;
            }

            foreach (SdlPlayer setPlayer in set.AllPlayers)
            {
                await sdlGuild.GetUser(setPlayer.DiscordId).AddRoleAsync(setRole);
            }

            await setChannel.SendMessageAsync(
                $"Welcome to set #{set.SetNumber}! To begin, {sdlGuild.GetUser(set.BravoTeam.Captain.DiscordId).Mention} will have two minutes to pick a player using `%pick [player]`.",
                embed: set.GetEmbedBuilder().Build());

            set.DraftTimeout += this.NewMatch_Closed;
        }
    }
}
