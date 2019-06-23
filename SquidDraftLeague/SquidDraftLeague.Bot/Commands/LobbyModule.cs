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
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Bot.Queuing;
using SquidDraftLeague.Language.Resources;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Lobby"), CheckPenalty, Group, RequireChannel(572536965833162753)]
    public class LobbyModule : InteractiveBase
    {
        public static readonly ReadOnlyCollection<Lobby> Lobbies = new List<Lobby>
        {
            new Lobby(1),
            new Lobby(2),
            new Lobby(3),
            new Lobby(4),
            new Lobby(5),
            new Lobby(6),
            new Lobby(7),
            new Lobby(8),
            new Lobby(9),
            new Lobby(10)
        }.AsReadOnly();

        private static readonly ulong[] SetChannels =
        {
            572542086260457474,
            572542140949856278,
            572542164316192777,
            589955282365317151,
            589955337256173571
        };

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("fill"),
        RequireRole("Developer")]
        public async Task DebugPropagate()
        {
            SdlPlayer sdlPlayer = await AirTableClient.RetrieveSdlPlayer((IGuildUser) this.Context.User);

            await this.JoinLobby(sdlPlayer, true);
        }

        [Command("join"),
         Summary("Join an existing lobby near your power level, or creates a new lobby if none exist.")]
        public async Task Join(int? lobbyNum = null)
        {
            if (!(this.Context.User is IGuildUser))
                return;

            IGuildUser player = (IGuildUser)this.Context.User;

            if (SetModule.Sets.Any(e => e.AllPlayers.Any(f => f.DiscordId == player.Id)))
            {
                await this.ReplyAsync(Resources.JoinLobbyInSet);
                return;
            }

            if (Lobbies.Any(e => e.Players.Any(f => f.DiscordId == player.Id)))
            {
                await this.ReplyAsync(Resources.JoinLobbyInLobby);
                return;
            }

            Logger.Info("Retrieving player records from airtable.");

            SdlPlayer sdlPlayer;

            try
            {
                sdlPlayer = await AirTableClient.RetrieveSdlPlayer(player);
            }
            catch (SdlAirTableException exception)
            {
                Logger.Error(exception);
                await exception.OutputToDiscordUser(this.Context);
                return;
            }

            Logger.Info("Complete. Searching for existing lobbies.");

            await this.JoinLobby(sdlPlayer, lobbyNumber: lobbyNum);
        }

        private async Task JoinLobby(SdlPlayer sdlPlayer, bool debugFill = false, int? lobbyNumber = null)
        {
            try
            {
                Lobby matchedLobby;

                if (lobbyNumber != null)
                {
                    Lobby selectedLobby = Lobbies[lobbyNumber.Value - 1];

                    if (selectedLobby.IsWithinThreshold(sdlPlayer.PowerLevel))
                    {
                        matchedLobby = selectedLobby;
                    }
                    else if (sdlPlayer.PowerLevel > selectedLobby.LobbyPowerLevel + selectedLobby.CurrentDelta)
                    {
                        matchedLobby = selectedLobby;
                        selectedLobby.Halved = sdlPlayer.DiscordId;
                    }
                    else
                    {
                        await this.ReplyAsync($"You are not eligible to join lobby #{lobbyNumber}");
                        return;
                    }
                }
                else
                {
                    List<Lobby> matchedLobbies =
                        Lobbies.Where(e => !e.IsFull && e.IsWithinThreshold(sdlPlayer.PowerLevel)).ToList();

                    if (matchedLobbies.Any())
                    {
                        matchedLobby = matchedLobbies.OrderBy(e => Math.Abs(e.LobbyPowerLevel - sdlPlayer.PowerLevel))
                            .First();
                    }
                    else
                    {
                        if (Lobbies.All(e => e.Players.Any()))
                        {
                            await this.ReplyAsync(Resources.LobbiesFull);
                            return;
                        }

                        Logger.Info("Getting available lobby(s).");

                        Logger.Info("Selecting first empty lobby.");
                        matchedLobby = Lobbies.First(e => !e.Players.Any());
                    }
                }

                matchedLobby.AddPlayer(sdlPlayer);

                matchedLobby.RenewContext(this.Context);

                if (debugFill)
                {
                    foreach (SdlPlayer nextPlayer in (await AirTableClient.RetrieveAllSdlPlayers(this.Context)).Where(e => e != sdlPlayer).Take(7))
                    {
                        matchedLobby.AddPlayer(nextPlayer, true);
                    }
                }

                if (matchedLobby.IsFull)
                {
                    if (SetModule.Sets.All(e => e.AllPlayers.Any()))
                    {
                        matchedLobby.InStandby = true;
                        // TODO Left off language file stuff here
                        await this.ReplyAsync(string.Format(Resources.SetsFull, "<#579890960394354690>"),
                            embed: matchedLobby.GetEmbedBuilder().Build());
                        return;
                    }

                    Set newSet = SetModule.Sets.First(e => !e.AllPlayers.Any());
                    newSet.Closed += NewMatch_Closed;
                    newSet.MoveLobbyToSet(matchedLobby);
                    matchedLobby.Close();

                    SocketRole setRole =
                        this.Context.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({newSet.SetNumber})");
                    SocketRole devRole = this.Context.Guild.Roles.First(e => e.Name == "Developer");

                    if (setRole == null)
                    {
                        await this.ReplyAsync(
                            $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({newSet.SetNumber})\".");
                        return;
                    }

                    foreach (SdlPlayer setPlayer in newSet.AllPlayers)
                    {
                        await this.Context.Guild.GetUser(setPlayer.DiscordId).AddRoleAsync(setRole);
                    }

                    await this.ReplyAsync($"Lobby filled! Please move to <#{SetChannels[newSet.SetNumber - 1]}>.");

                    await setRole.ModifyAsync(e => e.Mentionable = true);
                    RestUserMessage lastMessage = await this.Context.Guild.GetTextChannel(SetChannels[newSet.SetNumber - 1]).SendMessageAsync(
                        $"{setRole.Mention} Welcome to set #{newSet.SetNumber}! To begin, {this.Context.Guild.GetUser(newSet.BravoTeam.Captain.DiscordId).Mention} will have one minute to pick a player using `%pick [player]`.",
                        embed: newSet.GetEmbedBuilder().Build());
                    await setRole.ModifyAsync(e => e.Mentionable = false);

                    newSet.SetupTimeout(this.Context.Guild.GetTextChannel(SetChannels[newSet.SetNumber - 1]));
                }
                else
                {
                    string message =
                        $"{sdlPlayer.DiscordId.GetGuildUser(this.Context).Mention} has been added to Lobby #{matchedLobby.LobbyNumber}. {8 - matchedLobby.Players.Count} players needed to begin.";

                    IRole notifRole = this.Context.Guild.GetRole(592448366831730708);

                    if (matchedLobby.Players.Count == 1)
                    {
                        await notifRole.ModifyAsync(e => e.Mentionable = true);

                        message = $"{notifRole.Mention} A new lobby has been started! " + message;

                        await notifRole.ModifyAsync(e => e.Mentionable = false);
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

        private static async void NewMatch_Closed(object sender, Set set)
        {
            set.Closed -= NewMatch_Closed;

            if (!Lobbies.Any(l => l.InStandby)) return;

            Lobby matchedLobby = Lobbies.First(l => l.InStandby);
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

            RestUserMessage sendMessageAsync = await setChannel.SendMessageAsync(
                $"Welcome to set #{set.SetNumber}! To begin, {sdlGuild.GetUser(set.BravoTeam.Captain.DiscordId).Mention} will have two minutes to pick a player using `%pick [player]`.",
                embed: set.GetEmbedBuilder().Build());

            set.SetupTimeout((ITextChannel) sendMessageAsync.Channel);
        }
    }
}
