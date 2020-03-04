using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using NLog;
using SquidDraftLeague.Bot.Commands.Attributes;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.Draft.Penalties;
using SquidDraftLeague.Language.Resources;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    public class SharedDraftModule
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("cleanup"),
         RequirePermissions(Permissions.ManageGuild)]
        public async Task Cleanup(CommandContext ctx)
        {
            foreach (DiscordMember socketGuildUser in await ctx.Guild.GetAllMembersAsync())
            {
                if (socketGuildUser.IsBot)
                    continue;

                foreach (ulong cleanupRoleId in CommandHelper.DraftRoleIds)
                {
                    if (socketGuildUser.Roles.All(e => e.Id != cleanupRoleId))
                        continue;

                    DiscordRole role = ctx.Guild.GetRole(cleanupRoleId);
                    await socketGuildUser.RevokeRoleAsync(role);
                }
            }

            await ctx.RespondAsync("🦑");
        }

        /*[Command("fixTimeouts"),
         RequireOwner]
        public async Task FixTimeouts(CommandContext ctx)
        {   
            try
            {
                DateTime initiationTime = new DateTime(2019, 8, 14, 16, 0, 0, DateTimeKind.Utc);

                List<DiscordMessage> messages = null;
                DiscordMessage lastMessage = ctx.Message;
                string activityDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Player Activity")).FullName;

                while (messages == null || messages.All(x => x.Timestamp.UtcDateTime > initiationTime))
                {
                    Logger.Info($"Retriveing messages before message with timestamp {lastMessage.Timestamp}");
                    messages = (await ctx.Channel.GetMessagesAsync(lastMessage, Direction.Before).FlattenAsync()).ToList();

                    foreach (DiscordMessage message in messages.Where(x =>
                        x.Content.Contains("Please try again by using") &&
                        x.Author.Id == ctx.Client.CurrentUser.Id &&
                        x.Timestamp.UtcDateTime > initiationTime))
                    {
                        foreach (ulong messageMentionedUserId in message.MentionedUserIds)
                        {
                            try
                            {
                                PlayerActivity playerActivity;
                                string playerFile = Path.Combine(activityDirectory, $"{messageMentionedUserId}.json");

                                if (File.Exists(playerFile))
                                {
                                    playerActivity =
                                        JsonConvert.DeserializeObject<PlayerActivity>(
                                            await File.ReadAllTextAsync(playerFile));
                                }
                                else
                                {
                                    playerActivity = new PlayerActivity
                                    {
                                        PlayedSets = new List<DateTime>(),
                                        Timeouts = new List<DateTime>()
                                    };
                                }

                                if (!playerActivity.Timeouts.Any() ||
                                    playerActivity.Timeouts.All(e => e.Date != message.Timestamp.UtcDateTime.Date))
                                {
                                    playerActivity.Timeouts.Add(message.Timestamp.UtcDateTime);
                                }

                                await File.WriteAllTextAsync(playerFile, JsonConvert.SerializeObject(playerActivity));
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e);
                            }
                        }
                    }

                    lastMessage = messages.OrderByDescending(x => x.Timestamp).First();
                }

                await ctx.RespondAsync("🦑");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }*/

        [Command("statusall"),
         Description("Gets the status of all lobbies.")]
        public async Task StatusAll(CommandContext ctx)
        {
            if (Matchmaker.Lobbies.All(e => !e.Players.Any()))
            {
                await ctx.RespondAsync("There are currently no lobbies with players.");
                return;
            }

            foreach (Lobby lobby in Matchmaker.Lobbies.Where(e => e.Players.Any()))
            {
                await ctx.RespondAsync(embed: lobby.GetEmbedBuilder().Build());
            }
        }

        [Command("status"),
         Description("Gets the status of a lobby or set.")]
        public async Task Status(CommandContext ctx,
            [Description(
                "Optional parameter to specify a lobby number. Not required for sets or if you are checking a lobby you are in.")]
            int lobbyNum = 0)
        {
            if (!(ctx.User is DiscordMember user))
                return;

            Set setInChannel = CommandHelper.SetFromChannel(ctx.Channel.Id);

            if (setInChannel != null && setInChannel.AllPlayers.Any())
            {
                await ctx.RespondAsync(embed: setInChannel.GetEmbedBuilder().Build());
            }
            else if (Matchmaker.Lobbies.All(e => e.LobbyNumber != lobbyNum))
            {
                Lobby joinedLobby = Matchmaker.Lobbies.FirstOrDefault(e => e.Players.Any(f => f.DiscordId == user.Id));

                if (joinedLobby == null)
                {
                    await ctx.RespondAsync(Resources.NotInLobby);
                    return;
                }

                DiscordEmbedBuilder builder = joinedLobby.GetEmbedBuilder();

                await ctx.RespondAsync(embed: builder.Build());
            }
            else
            {
                Lobby selectedLobby = Matchmaker.Lobbies.First(e => e.LobbyNumber == lobbyNum);
                await ctx.RespondAsync(embed: selectedLobby.GetEmbedBuilder().Build());
            }
        }

        [Command("close"),
         RequirePermissions(Permissions.ManageGuild)]
        public async Task Close(CommandContext ctx, string type, int number)
        {
            type = type.ToLower();

            if (type == "set")
            {
                await ctx.RespondAsync("Closing set and removing roles please wait...");

                Set set = Matchmaker.Sets[number - 1];

                List<DiscordRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => ctx.Guild.GetRole(e)).ToList();

                foreach (SdlPlayer sdlPlayer in set.AllPlayers)
                {
                    await (await ctx.Guild.GetMemberAsync(sdlPlayer.DiscordId)).RemoveRolesAsync(roleRemovalList);
                }

                decimal points = await MatchModule.ReportScores(set, true);

                DiscordEmbed setEmbed = set.GetScoreEmbedBuilder(points, 0).Build();

                await ctx.RespondAsync($"An admin has closed set number {number}.", embed: setEmbed);

                set.Close();
            }
            else if (type == "lobby")
            {
                Matchmaker.Lobbies[number - 1].Close();
                await ctx.RespondAsync($"An admin has closed lobby number {number}.");
            }
            else
            {
                await ctx.RespondAsync("Please specify \"set\" or \"lobby\".");
            }
        }

        [Command("report"),
         Description("Reports a user for any reason. Only to be used in DMs with this bot."),
         ExampleCommand("%report \"DeltaJordan#5497\" Called me a crayon eater.")]
        public async Task Report(CommandContext ctx,
            [Description("Name formatted like so `\"DeltaJordan#5497\"`. The quotes are required if the name has spaces.")]
            string username, 
            [Description("The reason this person is being reported. Explain as much as possible."),
             RemainingText] string reason)
        {
            if (!ctx.Channel.IsPrivate)
            {
                return;
            }

            string[] splitName = username.Replace("@", "").Split('#');

            if (splitName.Length < 2)
            {
                await ctx.RespondAsync(Resources.InvalidReportNameSplit);
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                await ctx.RespondAsync(Resources.InvalidReportNoReason);
            }

            DiscordUser reportedUser = null;

            try
            {
                reportedUser = Program.Client.GetGuildAsync(570743985530863649).Result.Members
                    .First(x => x.Username == splitName[0] && x.Discriminator == splitName[1]);
            }
            catch (Exception e)
            {
                Logger.Trace(e);
            }

            if (reportedUser == null)
            {
                await ctx.RespondAsync(Resources.InvalidReportNameResolve);
            }
            else
            {
                DiscordChannel modChannel = await Program.Client.GetChannelAsync(572608285904207875);

                DiscordEmbedBuilder builder = new DiscordEmbedBuilder
                {
                    Description = $"**{reportedUser.Mention} reported by {ctx.User.Mention}.**",
                    Timestamp = DateTimeOffset.Now
                };

                builder.AddField(e =>
                {
                    e.Name = "Reason:";
                    e.Value = $"{reason}";
                });

                await modChannel.SendMessageAsync($"<@&572539082039885839>", embed: builder.Build());
                await ctx.RespondAsync(Resources.ReportResponse);
            }
        }

        [Command("penalty"),
        RequirePermissions(Permissions.ManageGuild)]
        public async Task Penalty(CommandContext ctx, DiscordMember user, int amount, [RemainingText] string notes)
        {
            try
            {
                if (amount <= 0)
                {
                    await ctx.RespondAsync("Amount should be greater than zero.");
                    return;
                }

                await MySqlClient.PenalizePlayer(await MySqlClient.RetrieveSdlPlayer(user.Id), amount, notes);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }

            await ctx.RespondAsync($"Penalized {user.Mention} {amount} points.");
        }

        [Command("kick"),
         RequireRole("Moderator")]
        public async Task Kick(CommandContext ctx, DiscordMember user, bool noPenalty = false)
        {
            Lobby joinedLobby = Matchmaker.Lobbies.FirstOrDefault(e => e.Players.Any(f => f.DiscordId == user.Id));

            if (joinedLobby != null)
            {
                joinedLobby.RemovePlayer(joinedLobby.Players.FirstOrDefault(e => e.DiscordId == user.Id));

                await ctx.RespondAsync($"{user.Mention} has been kicked from lobby #{joinedLobby.LobbyNumber}.");

                if (joinedLobby.Players.Count == 0)
                {
                    joinedLobby.Close();

                    await ctx.RespondAsync($"Lobby #{joinedLobby.LobbyNumber} has been disbanded.");
                }
            }
            else
            {
                Set joinedSet = Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == user.Id));

                decimal points = await MatchModule.ReportScores(joinedSet, true);

                if (joinedSet == null)
                {
                    return;
                }

                if (!noPenalty)
                {
                    string penaltyDir = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Penalties")).FullName;
                    string penaltyFile = Path.Combine(penaltyDir, $"{user.Id}.penalty");
                    Record record;

                    if (File.Exists(penaltyFile))
                    {
                        record = JsonConvert.DeserializeObject<Record>(File.ReadAllText(penaltyFile));
                    }
                    else
                    {
                        record = new Record
                        {
                            AllInfractions = new List<Infraction>()
                        };
                    }

                    // await AirTableClient.PenalizePlayer(user.Id, (int) (10 + points / 2), "Was kicked from a set.");

                    record.AllInfractions.Add(new Infraction
                    {
                        Penalty = (int) (10 + points / 2),
                        Notes = "Was kicked from a set.",
                        TimeOfOffense = DateTime.Now
                    });

                    File.WriteAllText(penaltyFile, JsonConvert.SerializeObject(record, Formatting.Indented));
                }

                if (joinedSet.AlphaTeam.Players.Any(e => e.DiscordId == user.Id))
                {
                    joinedSet.AlphaTeam.RemovePlayer(
                        joinedSet.AlphaTeam.Players.First(e => e.DiscordId == user.Id));
                }
                else if (joinedSet.BravoTeam.Players.Any(e => e.DiscordId == user.Id))
                {
                    joinedSet.BravoTeam.RemovePlayer(
                        joinedSet.BravoTeam.Players.First(e => e.DiscordId == user.Id));
                }
                else if (joinedSet.DraftPlayers.Any(e => e.DiscordId == user.Id))
                {
                    joinedSet.DraftPlayers.Remove(
                        joinedSet.DraftPlayers.First(e => e.DiscordId == user.Id));
                }

                List<DiscordMember> remainingUsers =
                    ctx.Guild.Members.Where(x => joinedSet.AllPlayers.Any(y => y.DiscordId == x.Id)).ToList();

                await ctx.RespondAsync(
                    $"{user.Mention} was kicked from the set. " +
                    $"Beginning removal of access to this channel in 30 seconds. " +
                    $"Rate limiting may cause the full process to take up to two minutes.",
                    embed: joinedSet.GetScoreEmbedBuilder(points, 0).Build());

                #region RemovedForBug

                /*Lobby movedLobby = Matchmaker.Lobbies.First(e => !e.Players.Any());

                if (movedLobby == null)
                {
                    // TODO Not sure what to do if all lobbies are filled.
                    return;
                }

                foreach (SdlPlayer joinedSetPlayer in joinedSet.AllPlayers)
                {
                    movedLobby.AddPlayer(joinedSetPlayer, true);
                }*/

                #endregion

                joinedSet.Close();

                await Task.Delay(TimeSpan.FromSeconds(30));

                List<DiscordRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => ctx.Guild.GetRole(e)).ToList();

                await user.RemoveRolesAsync(roleRemovalList.Where(x => user.Roles.Select(xr => xr.Id).Contains(x.Id)));

                foreach (DiscordMember member in remainingUsers)
                {
                    await member.RemoveRolesAsync(roleRemovalList.Where(x => member.Roles.Any(f => f.Id == x.Id)));
                }
                
                #region AlsoRemoved
                /*await ((ITextChannel)ctx.Client.GetChannel(572536965833162753))
                    .SendMessageAsync($"{8 - movedLobby.Players.Count} players needed to begin.",
                        embed: movedLobby.GetEmbedBuilder().Build());*/
                #endregion
            }
        }

        [Command("leave"),
         Description("Leaves a currently joined lobby.")]
        public async Task Leave(CommandContext ctx)
        {
            try
            {
                if (!(ctx.User is DiscordMember user))
                    return;

                Lobby joinedLobby = Matchmaker.Lobbies.FirstOrDefault(e => e.Players.Any(f => f.DiscordId == user.Id));

                if (joinedLobby != null)
                {
                    joinedLobby.RemovePlayer(joinedLobby.Players.FirstOrDefault(e => e.DiscordId == user.Id));

                    await ctx.RespondAsync($"You have left lobby #{joinedLobby.LobbyNumber}.");

                    if (joinedLobby.Players.Count == 0)
                    {
                        joinedLobby.Close();

                        await ctx.RespondAsync($"Lobby #{joinedLobby.LobbyNumber} has been disbanded.");
                    }
                }
                else
                {
                    Set joinedSet = Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == user.Id));

                    if (joinedSet == null)
                    {
                        return;
                    }

                    if (ctx.Channel.Id != (await CommandHelper.ChannelFromSet(joinedSet.SetNumber)).Id)
                    {
                        return;
                    }

                    decimal penalty = MatchModule.CalculatePoints(joinedSet) / 2 + 10;

                    string penaltyDir = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Penalties")).FullName;
                    string penaltyFile = Path.Combine(penaltyDir, $"{user.Id}.penalty");

                    string penaltyMessage = $"If you leave the set, you will be instated with a penalty of {penalty} points. ";
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

                    await ctx.RespondAsync(penaltyMessage + "\nAre you sure you wish to leave the set? (Y/N)");

                    InteractivityModule interactivity = ctx.Client.GetInteractivityModule();

                    MessageContext response =
                        await interactivity.WaitForMessageAsync(x => user.Id == x.Author.Id, TimeSpan.FromMinutes(1));

                    if (response == null)
                    {
                        await ctx.RespondAsync($"{user.Mention} took too long to respond. Assuming you changed your mind, please continue with the set.");
                    }
                    else if (response.Message.Content.ToLower() == "y")
                    {
                        decimal points = await MatchModule.ReportScores(joinedSet, true);
                        
                        await MySqlClient.PenalizePlayer(await MySqlClient.RetrieveSdlPlayer(user.Id), (int) penalty, "Left a set.");

                        record.AllInfractions.Add(new Infraction
                        {
                            Penalty = (int) penalty,
                            Notes = "Left a set.",
                            TimeOfOffense = DateTime.Now
                        });

                        if (joinedSet.AlphaTeam.Players.Any(e => e.DiscordId == user.Id))
                        {
                            joinedSet.AlphaTeam.RemovePlayer(
                                joinedSet.AlphaTeam.Players.First(e => e.DiscordId == user.Id));
                        }
                        else if (joinedSet.BravoTeam.Players.Any(e => e.DiscordId == user.Id))
                        {
                            joinedSet.BravoTeam.RemovePlayer(
                                joinedSet.BravoTeam.Players.First(e => e.DiscordId == user.Id));
                        }
                        else if (joinedSet.DraftPlayers.Any(e => e.DiscordId == user.Id))
                        {
                            joinedSet.DraftPlayers.Remove(
                                joinedSet.DraftPlayers.First(e => e.DiscordId == user.Id));
                        }

                        List<DiscordMember> remainingUsers =
                            ctx.Guild.Members.Where(x => joinedSet.AllPlayers.Any(y => y.DiscordId == x.Id)).ToList();

                        File.WriteAllText(penaltyFile, JsonConvert.SerializeObject(record, Formatting.Indented));

                        await ctx.RespondAsync(
                            $"{user.Mention} Aforementioned penalty applied. Don't make a habit of this! " +
                            $"As for the rest of the set, you will return to <#572536965833162753> to requeue. " +
                            $"Beginning removal of access to this channel in 30 seconds. " +
                            $"Rate limiting may cause the full process to take up to two minutes.",
                            embed: joinedSet.GetScoreEmbedBuilder(points, points / 2).Build());

                        /*Lobby movedLobby = Matchmaker.Lobbies.First(e => !e.Players.Any());

                        if (movedLobby == null)
                        {
                            // TODO Not sure what to do if all lobbies are filled.
                            return;
                        }

                        foreach (SdlPlayer joinedSetPlayer in joinedSet.AllPlayers)
                        {
                            movedLobby.AddPlayer(joinedSetPlayer, true);
                        }*/

                        joinedSet.Close();

                        await Task.Delay(TimeSpan.FromSeconds(30));

                        List<DiscordRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => ctx.Guild.GetRole(e)).ToList();

                        await user.RemoveRolesAsync(roleRemovalList.Where(x => user.Roles.Select(xr => xr.Id).Contains(x.Id)));

                        foreach (DiscordMember member in remainingUsers)
                        {
                            await member.RemoveRolesAsync(roleRemovalList.Where(x => member.Roles.Any(f => f.Id == x.Id)));
                        }

                        /*await (await ctx.Client.GetChannelAsync(572536965833162753))
                            .SendMessageAsync($"{8 - movedLobby.Players.Count} players needed to begin.", 
                                embed: movedLobby.GetEmbedBuilder().Build());*/
                    }
                    else
                    {
                        await ctx.RespondAsync("Assuming you declined leaving since you did not reply with \"Y\". Please continue with the set.");
                    }
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
