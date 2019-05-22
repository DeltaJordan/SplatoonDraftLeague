using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Plus0_Bot.AirTable;
using Plus0_Bot.Penalties;
using Plus0_Bot.Queuing;

namespace Plus0_Bot.Commands
{
    public class SharedDraftModule : InteractiveBase
    {
        [Command("leave", RunMode = RunMode.Async),
         Summary("Leaves a currently joined lobby.")]
        public async Task Leave()
        {
            if (!(this.Context.User is IGuildUser user))
                return;

            Lobby joinedLobby = LobbyModule.Lobbies.FirstOrDefault(e => e.Players.Any(f => f.DiscordUser.Id == user.Id));

            if (joinedLobby != null)
            {
                SocketRole setRole = this.Context.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({joinedLobby.LobbyNumber})");
                SocketRole devRole = this.Context.Guild.Roles.First(e => e.Name == "Developer");

                if (setRole == null)
                {
                    await devRole.ModifyAsync(e => e.Mentionable = true);
                    await this.Context.Channel.SendMessageAsync($"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({joinedLobby.LobbyNumber})\".");
                    await devRole.ModifyAsync(e => e.Mentionable = false);
                    return;
                }

                await user.RemoveRoleAsync(setRole);
                joinedLobby.RemovePlayer(joinedLobby.Players.FirstOrDefault(e => e.DiscordUser.Id == user.Id));

                await this.ReplyAsync($"You have left lobby #{joinedLobby.LobbyNumber}.");

                if (joinedLobby.Players.Count == 0)
                {
                    joinedLobby.Close();

                    await this.ReplyAsync($"Lobby #{joinedLobby.LobbyNumber} has been disbanded.");
                }
            }
            else
            {
                Set joinedSet = SetModule.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordUser.Id == user.Id));

                if (joinedSet == null)
                {
                    return;
                }

                string penaltyDir = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Penalties")).FullName;
                string penaltyFile = Path.Combine(penaltyDir, $"{user.Id}.penalty");

                string penaltyMessage = "If you leave the set, you will be instated with a penalty of 15 points. ";
                Record record;

                if (File.Exists(penaltyFile))
                {
                    record = JsonConvert.DeserializeObject<Record>(File.ReadAllText(penaltyFile));

                    int infractionCount = record.InfractionsThisMonth() + 1;

                    switch (infractionCount)
                    {
                        case 1:
                            penaltyMessage += "";
                            break;
                        case 2:
                            penaltyMessage += "In addition, you will be banned from participating in SDL for 24 hours.";
                            break;
                        case 3:
                            penaltyMessage += "In addition, you will be banned from participating in SDL for 1 week.";
                            break;
                        default:
                            penaltyMessage +=
                                "In addition, you will be banned from participating in SDL for 1 week AND will be barred from participating in cups.";
                            break;
                    }
                }
                else
                {
                    penaltyMessage += " Otherwise, this time will be just a warning.";

                    record = new Record
                    {
                        AllInfractions = new List<Infraction>()
                    };
                }

                await this.ReplyAsync(penaltyMessage + " Are you sure you wish to leave the set? (Y/N)");

                SocketMessage response = await this.NextMessageAsync(timeout: TimeSpan.FromMinutes(1));

                if (response == null)
                {
                    await this.ReplyAsync($"{user.Mention} took too long to respond. Assuming you changed your mind, please continue with the set.");
                }
                else if (response.Content.ToLower() == "y")
                {
                    // TODO Apply penalty to database.

                    record.AllInfractions.Add(new Infraction
                    {
                        Penalty = 15,
                        Notes = "Left a set.",
                        TimeOfOffense = DateTime.Now
                    });

                    if (joinedSet.AlphaTeam.Players.Any(e => e.DiscordUser.Id == user.Id))
                    {
                        joinedSet.AlphaTeam.RemovePlayer(
                            joinedSet.AlphaTeam.Players.First(e => e.DiscordUser.Id == user.Id));
                    }
                    else if (joinedSet.BravoTeam.Players.Any(e => e.DiscordUser.Id == user.Id))
                    {
                        joinedSet.BravoTeam.RemovePlayer(
                            joinedSet.BravoTeam.Players.First(e => e.DiscordUser.Id == user.Id));
                    }
                    else if (joinedSet.DraftPlayers.Any(e => e.DiscordUser.Id == user.Id))
                    {
                        joinedSet.DraftPlayers.Remove(
                            joinedSet.DraftPlayers.First(e => e.DiscordUser.Id == user.Id));
                    }

                    File.WriteAllText(penaltyFile, JsonConvert.SerializeObject(record, Formatting.Indented));

                    await this.ReplyAsync(
                        $"{user.Mention} Aforementioned penalty applied. Don't make a habit of this! " +
                        $"As for the rest of the set, you will return to <#572536965833162753> to requeue. Removing access to this channel in 30 seconds.");

                    Lobby movedLobby = LobbyModule.Lobbies.First(e => !e.Players.Any());

                    if (movedLobby == null)
                    {
                        // TODO Not sure what to do if all lobbies are filled.
                        return;
                    }

                    foreach (SdlPlayer joinedSetPlayer in joinedSet.AllPlayers)
                    {
                        movedLobby.AddPlayer(joinedSetPlayer, true);
                    }

                    joinedSet.Close();

                    SocketRole setRole = this.Context.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({joinedSet.SetNumber})");
                    SocketRole devRole = this.Context.Guild.Roles.First(e => e.Name == "Developer");

                    if (setRole == null)
                    {
                        await devRole.ModifyAsync(e => e.Mentionable = true);
                        await this.ReplyAsync(
                            $"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({joinedSet.SetNumber})\".");
                        await devRole.ModifyAsync(e => e.Mentionable = false);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30));

                    foreach (SdlPlayer movedLobbyPlayer in movedLobby.Players)
                    {
                        await movedLobbyPlayer.DiscordUser.RemoveRoleAsync(setRole);
                    }

                    await ((ITextChannel) this.Context.Client.GetChannel(572536965833162753))
                        .SendMessageAsync($"{8 - movedLobby.Players.Count} players needed to begin.", 
                            embed: movedLobby.GetEmbedBuilder().Build());
                }
                else
                {
                    await this.ReplyAsync("K. Please continue with the set.");
                }
            }
        }
    }
}
