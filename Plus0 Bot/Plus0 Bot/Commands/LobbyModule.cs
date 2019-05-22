using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Plus0_Bot.AirTable;
using Plus0_Bot.Commands.Preconditions;
using Plus0_Bot.Queuing;

namespace Plus0_Bot.Commands
{
    public class LobbyModule : ModuleBase<SocketCommandContext>
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
            572542164316192777
        };

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("status"),
         Summary("Gets the status of a lobby.")]
        public async Task Status([Summary(
            "Optional parameter to specify a lobby number. " +
            "Not required if you are checking a lobby you are in.")]
            int lobbyNum = 0)
        {
            if (!(this.Context.User is IGuildUser user))
                return;

            if (Lobbies.All(e => e.LobbyNumber != lobbyNum))
            {
                Lobby joinedLobby = Lobbies.FirstOrDefault(e => e.Players.Any(f => f.DiscordUser.Id == user.Id));

                if (joinedLobby == null)
                {
                    await this.ReplyAsync("You are not in a lobby!");
                    return;
                }

                EmbedBuilder builder = joinedLobby.GetEmbedBuilder();

                await this.ReplyAsync(embed: builder.Build());
            }
            else
            {
                Lobby selectedLobby = Lobbies.First(e => e.LobbyNumber == lobbyNum);
                await this.ReplyAsync(embed: selectedLobby.GetEmbedBuilder().Build());
            }
        }

        [Command("fill"),
        RequireRole("Developer")]
        public async Task DebugPropagate()
        {
            try
            {
                SdlPlayer sdlPlayer = new SdlPlayer((IGuildUser) this.Context.User) {PowerLevel = 1900};

                List<Lobby> matchedLobbies = Lobbies.Where(e => !e.IsFull && e.IsWithinThreshold(sdlPlayer.PowerLevel)).ToList();
                Lobby matchedLobby;
                if (matchedLobbies.Any())
                {
                    matchedLobby = matchedLobbies.OrderBy(e => Math.Abs(e.LobbyPowerLevel - sdlPlayer.PowerLevel)).First();

                    matchedLobby.AddPlayer(sdlPlayer);
                }
                else
                {
                    if (Lobbies.All(e => e.Players.Any()))
                    {
                        await this.ReplyAsync($"There are already three lobbies! Please wait until another lobby " +
                                              $"either enters your power level threshold or there are less than 3 queuing lobbies.");
                        return;
                    }

                    Logger.Info("Getting available lobby(s).");

                    Logger.Info("Selecting first empty lobby.");
                    matchedLobby = Lobbies.First(e => !e.Players.Any());
                }

                for (int i = 0; i < 7; i++)
                {
                    SdlPlayer nextPlayer = new SdlPlayer(this.Context.Guild.Users.Where(e => e.Id != this.Context.User.Id).ElementAt(i)) {PowerLevel = 1900 + i};

                    matchedLobby.AddPlayer(nextPlayer, true);
                }

                if (matchedLobby.IsFull)
                {
                    if (SetModule.Sets.All(e => e.AllPlayers.Any()))
                    {
                        await this.ReplyAsync("Sit tight in <#579890960394354690>! There are too many sets in progress right now. Once a set finishes you will be notified.",
                            embed: matchedLobby.GetEmbedBuilder().Build());
                        return;
                    }

                    Set newMatch = SetModule.Sets.First(e => !e.AllPlayers.Any());
                    newMatch.MoveLobbyToSet(matchedLobby);
                    matchedLobby.Close();

                    SocketRole setRole = this.Context.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({newMatch.SetNumber})");
                    SocketRole devRole = this.Context.Guild.Roles.First(e => e.Name == "Developer");

                    if (setRole == null)
                    {
                        await devRole.ModifyAsync(e => e.Mentionable = true);
                        await this.ReplyAsync(
                            $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({newMatch.SetNumber})\".");
                        await devRole.ModifyAsync(e => e.Mentionable = false);
                        return;
                    }

                    await this.Context.Guild.GetTextChannel(SetChannels[newMatch.SetNumber - 1]).SendMessageAsync(
                        $"Welcome to set #{newMatch.SetNumber}! To begin, {newMatch.BravoTeam.Captain.DiscordUser.Mention} will pick players using `%pick [player]`.",
                        embed: newMatch.GetEmbedBuilder().Build());
                }
                else
                {
                    string message =
                        $"{sdlPlayer.DiscordUser.Mention} has been added to Lobby #{matchedLobby.LobbyNumber}. {8 - matchedLobby.Players.Count} players needed to begin.";

                    EmbedBuilder builder = matchedLobby.GetEmbedBuilder();

                    await this.ReplyAsync(message, false, builder.Build());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("join"),
         Summary("Join an existing lobby near your power level, or creates a new lobby if none exist.")]
        public async Task Join()
        {
            if (!(this.Context.User is IGuildUser))
                return;

            IGuildUser player = (IGuildUser)this.Context.User;

            if (SetModule.Sets.Any(e => e.AllPlayers.Any(f => f.DiscordUser.Id == player.Id)))
            {
                await this.ReplyAsync("You are already in a set, there is no need to join a lobby.");
                return;
            }

            if (Lobbies.Any(e => e.Players.Any(f => f.DiscordUser.Id == player.Id)))
            {
                await this.ReplyAsync("You cannot join a lobby if you are already in one!");
                return;
            }

            Logger.Info("Retrieving player records from airtable.");
            SdlPlayer sdlPlayer = await AirTableClient.RetrievePlayer(player);

            if (sdlPlayer == null)
            {
                await this.ReplyAsync("You do not appear to be registered.");
                return;
            }

            Logger.Info("Complete. Searching for existing lobbies.");
            List<Lobby> matchedLobbies = Lobbies.Where(e => !e.IsFull && e.IsWithinThreshold(sdlPlayer.PowerLevel)).ToList();

            Lobby matchedLobby;
            if (matchedLobbies.Any())
            {
                matchedLobby = matchedLobbies.OrderBy(e => Math.Abs(e.LobbyPowerLevel - sdlPlayer.PowerLevel)).First();

                matchedLobby.AddPlayer(sdlPlayer);
            }
            else
            {
                if (Lobbies.All(e => e.Players.Any()))
                {
                    await this.ReplyAsync($"There are already three lobbies! Please wait until another lobby " +
                                          $"either enters your power level threshold or there are less than 3 queuing lobbies.");
                    return;
                }

                Logger.Info("Getting available lobby(s).");

                Logger.Info("Selecting first empty lobby.");
                matchedLobby = Lobbies.First(e => !e.Players.Any());
            }

            if (matchedLobby.IsFull)
            {
                if (SetModule.Sets.All(e => e.AllPlayers.Any()))
                {
                    await this.ReplyAsync("Sit tight in <#579890960394354690>! There are too many sets in progress right now. Once a set finishes you will be notified.",
                                          embed: matchedLobby.GetEmbedBuilder().Build());
                    return;
                }

                Set newMatch = SetModule.Sets.First(e => !e.AllPlayers.Any());
                newMatch.MoveLobbyToSet(matchedLobby);
                matchedLobby.Close();

                SocketRole setRole = this.Context.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({newMatch.SetNumber})");
                SocketRole devRole = this.Context.Guild.Roles.First(e => e.Name == "Developer");

                if (setRole == null)
                {
                    await devRole.ModifyAsync(e => e.Mentionable = true);
                    await this.ReplyAsync(
                        $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({newMatch.SetNumber})\".");
                    await devRole.ModifyAsync(e => e.Mentionable = false);
                    return;
                }

                foreach (SdlPlayer matchedLobbyPlayer in matchedLobby.Players)
                {
                    await matchedLobbyPlayer.DiscordUser.AddRoleAsync(setRole);
                }

                await this.Context.Guild.GetTextChannel(SetChannels[newMatch.SetNumber - 1]).SendMessageAsync(
                    $"Welcome to set #{newMatch.SetNumber}! To begin, {newMatch.BravoTeam.Captain.DiscordUser.Mention} will pick players using `%pick [player]`.", 
                    embed: newMatch.GetEmbedBuilder().Build());
            }
            else
            {
                string message =
                    $"{player.Mention} has been added to Lobby #{matchedLobby.LobbyNumber}. {8 - matchedLobby.Players.Count} players needed to begin.";

                EmbedBuilder builder = matchedLobby.GetEmbedBuilder();

                await this.ReplyAsync(message, false, builder.Build());
            }
        }
    }
}
