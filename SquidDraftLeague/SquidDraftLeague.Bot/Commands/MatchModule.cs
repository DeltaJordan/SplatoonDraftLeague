using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using SquidDraftLeague.Bot.Commands.Attributes;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Map;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.Language.Resources;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Match")]
    public class MatchModule : InteractiveBase<SocketCommandContext>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // NOTE This is where we'll have to implement the different match amounts for cups.
        private const int SET_MATCH_NUMBER = 7;

        public static async Task MoveToMatch(SocketTextChannel context, Set set)
        {
            await context.SendMessageAsync("Both teams are drafted! Please wait while roles are being distributed...");

            IRole alphaRole = context.Guild.Roles.First(e => e.Name == $"Alpha ({set.SetNumber})");
            IRole bravoRole = context.Guild.Roles.First(e => e.Name == $"Bravo ({set.SetNumber})");

            foreach (SdlPlayer alphaTeamPlayer in set.AlphaTeam.Players)
            {
                await context.Guild.GetUser(alphaTeamPlayer.DiscordId).AddRoleAsync(alphaRole);
            }

            foreach (SdlPlayer bravoTeamPlayer in set.BravoTeam.Players)
            {
                await context.Guild.GetUser(bravoTeamPlayer.DiscordId).AddRoleAsync(bravoRole);
            }

            await alphaRole.ModifyAsync(e => e.Mentionable = true);
            await bravoRole.ModifyAsync(e => e.Mentionable = true);
            await context.SendMessageAsync($"{alphaRole.Mention} {bravoRole.Mention} To begin, please join your respective voice channels.\n" +
                                                   $"After everyone is situated, please create and join a Private Battle and have the host select the map and mode chosen after this message. " +
                                                   $"Once the match is over, the captain of the **losing team** will use %score to report the score like so:\n" +
                                                   $"`%score`");

            set.MatchNum = 1;

            SelectHostResponse selectHostResponse = Matchmaker.SelectHost(set);

            if (!selectHostResponse.Success || selectHostResponse.DiscordId == null)
            {
                if (selectHostResponse.Message != null)
                    await context.SendMessageAsync(selectHostResponse.Message);

                if (selectHostResponse.Exception != null)
                    Logger.Error(selectHostResponse.Exception);

                return;
            }

            IUser hostUser = context.GetUser(selectHostResponse.DiscordId.Value);

            set.Host = set.AllPlayers.First(e => e.DiscordId == hostUser.Id);

            Stage[] mapList = await MySqlClient.GetMapList();
            set.PickStages(mapList);
            Stage selectedStage = set.GetCurrentStage();

            EmbedBuilder embed = new EmbedBuilder
            {
                Title = $"Stages for Set #{set.SetNumber}",
                Description = 
                    string.Join("\n", set.Stages.Select(x => $"<:{x.GetModeEmote().Name}:{x.GetModeEmote().Id}> {x.MapName}"))
            };

            await context.Guild.GetTextChannel(601123765237186560).SendMessageAsync(embed: embed.Build());

            await context.SendMessageAsync(embed: selectedStage
                .GetEmbedBuilder($"Match 1 of 7: {selectedStage.MapName}")
                .AddField(e =>
                {
                    e.Name = "Alpha Team's Score";
                    e.Value = set.AlphaTeam.Score;
                    e.IsInline = true;
                })
                .AddField(e =>
                {
                    e.Name = "Bravo Team's Score";
                    e.Value = set.BravoTeam.Score;
                    e.IsInline = true;
                })
                .Build());

            await context.SendMessageAsync($"{hostUser.Mention} has been selected as host! The password will be **{hostUser.DiscriminatorValue}**. " +
                                           $"Note that if this person is not capable of hosting (due to internet connection etc.) " +
                                           $"it is fine for another person to do so, however keep in mind that situation permitting this person is **first choice** as host.");

            await context.SendMessageAsync("🦑Now then. let the games commence!🐙");

            IRole setRole = context.Guild.Roles.First(e => e.Name == $"In Set ({set.SetNumber})");

            foreach (SdlPlayer allPlayer in set.AllPlayers)
            {
                await context.Guild.GetUser(allPlayer.DiscordId).RemoveRoleAsync(setRole);
            }
        }

        [Command("canhost"),
         Summary("Toggles whether you wish to be a prioritized candidate to host sets.")]
        public async Task CanHost()
        {
            if (Matchmaker.ToggleCanHost(this.Context.User.Id))
            {
                await this.ReplyAsync("You are now a priority candidate to host during sets.");
            }
            else
            {
                await this.ReplyAsync("You are no longer a priority candidate to host during sets.");
            }
        }

        [Command("host"),
         Summary("Shows information about the host of this set.")]
        public async Task Host()
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == this.Context.User.Id));

            if (playerSet == null ||
                CommandHelper.ChannelFromSet(playerSet.SetNumber).Id != this.Context.Channel.Id)
            {
                return;
            }

            SocketGuildUser hostUser = this.Context.Guild.GetUser(playerSet.Host.DiscordId);

            EmbedBuilder builder = new EmbedBuilder();
            builder.AddField(e =>
            {
                e.Name = "Host";
                e.Value = hostUser.Mention;
                e.IsInline = true;
            });

            builder.AddField(e =>
            {
                e.Name = "Generated Password";
                e.Value = hostUser.DiscriminatorValue;
                e.IsInline = true;
            });

            await this.ReplyAsync(embed: builder.Build());
        }

        [Command("dispute"),
         Summary("Reports a discrepancy in a set to moderators.")]
        public async Task Dispute()
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == this.Context.User.Id));

            IRole modRole = this.Context.Guild.Roles.First(e => e.Name == "Moderator");

            if (playerSet == null)
            {
                await this.ReplyAsync(Resources.ReportErrorResponse);
                return;
            }

            await this.ReplyAsync($"{modRole.Mention} A dispute has been opened by {this.Context.User.Mention}!"+
                                  $"To resolve the error, use `%resolve` and follow the resulting instructions. " +
                                  $"Otherwise, use `%resolve deny` to continue reporting the current score.");

            playerSet.Locked = true;
        }

        [Command("resolve"),
         RequireRole("Moderator")]
        public async Task Resolve(string deny = null, int setNumber = 0)
        {
            Set playerSet;

            if (setNumber == 0)
            {
                playerSet = CommandHelper.SetFromChannel(this.Context.Channel.Id);

                if (playerSet == null)
                {
                    await this.ReplyAsync("Please specify set number or use the correct channel as context.");
                    return;
                }
            }
            else
            {
                playerSet = Matchmaker.Sets[setNumber - 1];
            }

            playerSet.Locked = false;

            if (deny == "deny")
            {
                if (playerSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                    playerSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                    playerSet.MatchNum == SET_MATCH_NUMBER)
                {
                    await this.EndMatchAsync(playerSet);

                    return;
                }

                await this.ReplyAsync("The report has been resolved by a moderator. This is a final decision. Please continue with the set.");

                Stage selectedRandomStage = playerSet.GetCurrentStage();

                await this.ReplyAsync(embed: selectedRandomStage
                    .GetEmbedBuilder($"Match {playerSet.MatchNum} of 7: {selectedRandomStage.MapName}")
                    .AddField(e =>
                    {
                        e.Name = "Alpha Team's Score";
                        e.Value = playerSet.AlphaTeam.Score;
                        e.IsInline = true;
                    })
                    .AddField(e =>
                    {
                        e.Name = "Bravo Team's Score";
                        e.Value = playerSet.BravoTeam.Score;
                        e.IsInline = true;
                    })
                    .Build());

                return;
            }

            playerSet.MatchNum--;

            playerSet.ResolveMode = playerSet.MatchNum;

            playerSet.MatchNum = 0;
            playerSet.AlphaTeam.OrderedMatchResults.Clear();
            playerSet.BravoTeam.OrderedMatchResults.Clear();

            Stage selectedStage = playerSet.Stages[0];
            await this.ReplyAsync("Moderator, use `%overwrite [Team]` to select the winner for this map.", 
                embed: selectedStage.GetEmbedBuilder($"Match 1 of 7: {selectedStage.MapName}")
                .AddField(e =>
                {
                    e.Name = "Alpha Team's Score";
                    e.Value = playerSet.AlphaTeam.Score;
                    e.IsInline = true;
                })
                .AddField(e =>
                {
                    e.Name = "Bravo Team's Score";
                    e.Value = playerSet.BravoTeam.Score;
                    e.IsInline = true;
                })
                .Build());
        }

        [Command("overwrite"),
         RequireRole("Moderator")]
        public async Task Overwrite(string team, int set = 0)
        {
            Set selectedSet;

            if (set == 0)
            {
                selectedSet = CommandHelper.SetFromChannel(this.Context.Channel.Id);

                if (selectedSet == null)
                {
                    await this.ReplyAsync("Please specify set number or use the correct channel as context.");
                    return;
                }
            }
            else
            {
                selectedSet = Matchmaker.Sets[set - 1];
            }

            team = team.ToLower();

            selectedSet.ReportScore(team);

            selectedSet.MatchNum++;
            selectedSet.ResolveMode--;

            if (selectedSet.ResolveMode == 0)
            {
                if (selectedSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                    selectedSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                    selectedSet.MatchNum == SET_MATCH_NUMBER)
                {
                    await this.ReplyAsync("Resolved all issues! All scores will be reported to Airtable.");

                    await this.EndMatchAsync(selectedSet);
                }
                else
                {
                    await this.ReplyAsync("Resolved all issues! Teams, continue with your matches.");

                    selectedSet.MatchNum++;

                    Stage selectedStage = selectedSet.Stages[selectedSet.MatchNum - 1];

                    await this.ReplyAsync(embed: selectedStage
                        .GetEmbedBuilder($"Match {selectedSet.MatchNum} of 7: {selectedStage.MapName}")
                        .AddField(e =>
                        {
                            e.Name = "Alpha Team's Score";
                            e.Value = selectedSet.AlphaTeam.Score;
                            e.IsInline = true;
                        })
                        .AddField(e =>
                        {
                            e.Name = "Bravo Team's Score";
                            e.Value = selectedSet.BravoTeam.Score;
                            e.IsInline = true;
                        })
                        .Build());
                }
            }
            else
            {
                Stage selectedStage = selectedSet.Stages[selectedSet.MatchNum];
                await this.ReplyAsync("Moderator, use `%overwrite [Team]` to select the winner for this map.",
                    embed: selectedStage.GetEmbedBuilder($"Match {selectedSet.MatchNum + 1} of 7: {selectedStage.MapName}")
                        .AddField(e =>
                        {
                            e.Name = "Alpha Team's Score";
                            e.Value = selectedSet.AlphaTeam.Score;
                            e.IsInline = true;
                        })
                        .AddField(e =>
                        {
                            e.Name = "Bravo Team's Score";
                            e.Value = selectedSet.BravoTeam.Score;
                            e.IsInline = true;
                        })
                        .Build());
            }
        }

        [Command("score")]
        public async Task Score()
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == this.Context.User.Id));

            IRole modRole = this.Context.Guild.Roles.First(e => e.Name == "Moderator");

            if (playerSet == null)
            {
                return;
            }

            if (playerSet.DraftPlayers.Any())
            {
                return;
            }

            if (playerSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                playerSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2)
            {
                return;
            }

            if (playerSet.Locked)
            {
                await this.ReplyAsync("This set will be locked until a moderator addresses the report. Please wait.");
                return;
            }

            if (playerSet.ResolveMode > 0)
            {
                return;
            }

            if (this.Context.Channel.Id != CommandHelper.ChannelFromSet(playerSet.SetNumber).Id)
            {
                return;
            }

            if (!playerSet.AlphaTeam.IsCaptain(this.Context.User.Id) && 
                !playerSet.BravoTeam.IsCaptain(this.Context.User.Id))
            {
                await this.ReplyAsync("Only the captain of the losing team can report the score.");
                return;
            }

            string team = playerSet.AlphaTeam.IsCaptain(this.Context.User.Id) ? "bravo" : "alpha";

            playerSet.ReportScore(team);

            if (playerSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                playerSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                playerSet.MatchNum == SET_MATCH_NUMBER)
            {
                string winner = playerSet.AlphaTeam.Score > playerSet.BravoTeam.Score ? "Alpha" : "Bravo";
                string loser = playerSet.AlphaTeam.Score < playerSet.BravoTeam.Score ? "Alpha" : "Bravo";

                EmbedBuilder builder = new EmbedBuilder();
                builder.WithTitle("__Results__");
                builder.AddField(e =>
                {
                    e.Name = "Score";
                    e.IsInline = true;
                    e.Value = $"{winner} Wins {playerSet.AlphaTeam.Score} - {playerSet.BravoTeam.Score}";
                });

                await this.ReplyAsync($"The winner of this set is Team {winner}!", embed: builder.Build());

                IRole loserRole = loser == "Alpha" ? 
                    this.Context.Guild.Roles.First(e => e.Name == $"Alpha ({playerSet.SetNumber})") : 
                    this.Context.Guild.Roles.First(e => e.Name == $"Bravo ({playerSet.SetNumber})");

                await this.ReplyAsync(
                    $"{loserRole.Mention}, please acknowledge these results by either sending \"confirm\" or \"deny\".");

                DateTime timeoutDateTime = DateTime.Now + TimeSpan.FromMinutes(2);
                SocketMessage replyMessage;
                try
                {
                    replyMessage = await this.NextMessageAsync(false, true, TimeSpan.FromMinutes(1));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Logger.Error(e);
                    throw;
                }

                while (true)
                {
                    if (replyMessage == null)
                    {
                        await this.ReplyAsync("Time's up! Assuming the losing team has accepted their loss.");

                        await this.EndMatchAsync(playerSet);

                        return;
                    }

                    SocketGuildUser authorGuildUser = (SocketGuildUser) replyMessage.Author;

                    if (!authorGuildUser.Roles.Select(e => e.Id).Contains(loserRole.Id))
                    {
                        if (DateTime.Now > timeoutDateTime)
                        {
                            await this.EndMatchAsync(playerSet);

                            return;
                        }

                        replyMessage = await this.NextMessageAsync(false, true, timeoutDateTime - DateTime.Now);
                        continue;
                    }

                    if (replyMessage.Content.ToLower() == "confirm")
                    {
                        await this.EndMatchAsync(playerSet);

                        return;
                    }

                    if (replyMessage.Content.ToLower() == "deny")
                    {
                        await this.ReplyAsync($"{modRole.Mention} issue reported by {replyMessage.Author.Mention}. " +
                                              $"To resolve the error, use `%resolve` and follow the resulting instructions." +
                                              $" Otherwise, use `%resolve deny` to continue reporting the current score.");
                        return;
                    }

                    replyMessage = await this.NextMessageAsync(false, true, timeoutDateTime - DateTime.Now);
                }
            }
            else
            {
                try
                {
                    playerSet.MatchNum++;

                    Stage selectedStage = playerSet.GetCurrentStage();

                    await this.ReplyAsync(embed: selectedStage
                        .GetEmbedBuilder($"Match {playerSet.MatchNum} of 7: {selectedStage.MapName}")
                        .AddField(e =>
                        {
                            e.Name = "Alpha Team's Score";
                            e.Value = playerSet.AlphaTeam.Score;
                            e.IsInline = true;
                        })
                        .AddField(e =>
                        {
                            e.Name = "Bravo Team's Score";
                            e.Value = playerSet.BravoTeam.Score;
                            e.IsInline = true;
                        })
                        .Build());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private async Task EndMatchAsync(Set playerSet)
        {
            decimal points = await ReportScores(playerSet);

            //TimePeriod happyPeriod = new TimePeriod(TimeSpan.Parse("20:00"), TimeSpan.Parse("21:00"));
            //TimePeriod halfPeriod = new TimePeriod(TimeSpan.Parse("1:00"), TimeSpan.Parse("2:00"));

            Embed setEmbed = playerSet.GetScoreEmbedBuilder(points,
                /*happyPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault()) ||
                halfPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault())
                    ? points / 2
                    :*/ points).Build();

            await this.ReplyAsync($"Congratulations Team {playerSet.Winning} on their victory! " +
                                  $"Everyone's power levels have been updated to reflect this match. " +
                                  $"Beginning removal of access to this channel in 30 seconds. " +
                                  $"Rate limiting may cause the full process to take up to two minutes.",
                embed: setEmbed);

            await Task.Delay(30000);

            List<SocketRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => this.Context.Guild.GetRole(e)).ToList();

            foreach (SdlPlayer playerSetAllPlayer in playerSet.AllPlayers)
            {
                SocketGuildUser guildUser = this.Context.Guild.GetUser(playerSetAllPlayer.DiscordId);

                await guildUser.RemoveRolesAsync(roleRemovalList.Where(e =>
                    this.Context.Guild.GetUser(guildUser.Id).Roles
                        .Any(f => e.Id == f.Id)));
            }

            playerSet.Close();

            IRole classOneRole = this.Context.Guild.GetRole(600770643075661824);
            IRole classTwoRole = this.Context.Guild.GetRole(600770814521901076);
            IRole classThreeRole = this.Context.Guild.GetRole(600770862307606542);
            IRole classFourRole = this.Context.Guild.GetRole(600770905282576406);

            IRole[] allClassRoles = { classOneRole, classTwoRole, classThreeRole, classFourRole };

            string optOutDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Opt Out")).FullName;

            foreach (SdlPlayer sdlPlayer in await MySqlClient.RetrieveAllSdlPlayers())
            {
                try
                {
                    SocketGuildUser sdlGuildUser = this.Context.Guild.GetUser(sdlPlayer.DiscordId);

                    if (File.Exists(Path.Combine(optOutDirectory, $"{sdlGuildUser.Id}.dat")))
                    {
                        continue;
                    }

                    switch (Matchmaker.GetClass(sdlPlayer.PowerLevel))
                    {
                        case SdlClass.Zero:
                            break;
                        case SdlClass.One:
                            if (sdlGuildUser.Roles.All(e => e.Id != classOneRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.AddRoleAsync(classOneRole);
                            }
                            break;
                        case SdlClass.Two:
                            if (sdlGuildUser.Roles.All(e => e.Id != classTwoRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.AddRoleAsync(classTwoRole);
                            }
                            break;
                        case SdlClass.Three:
                            if (sdlGuildUser.Roles.All(e => e.Id != classThreeRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.AddRoleAsync(classThreeRole);
                            }
                            break;
                        case SdlClass.Four:
                            if (sdlGuildUser.Roles.All(e => e.Id != classFourRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.AddRoleAsync(classFourRole);
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            // TODO Removed message clearing for the time being.
            if (this.Context.Channel.Id == 0 /*CommandHelper.ChannelFromSet(playerSet.SetNumber).Id*/)
            {
                IEnumerable<IMessage> messages = await this.Context.Channel.GetMessagesAsync(1000 + 1).FlattenAsync();

                await ((ITextChannel) this.Context.Channel).DeleteMessagesAsync(messages);

                IUserMessage reply =
                    await this.ReplyAsync(
                        "Cleared the channel of 1000 messages. This message will be deleted in 10 seconds as well.");

                await Task.Delay(10000);

                await reply.DeleteAsync();
            }
        }

        public static decimal CalculatePoints(Set playerSet)
        {
            decimal alphaPowerAverage = playerSet.AlphaTeam.Players.Select(e => e.PowerLevel).Average();
            decimal bravoPowerAverage = playerSet.BravoTeam.Players.Select(e => e.PowerLevel).Average();

            decimal points;
            switch (playerSet.Winning)
            {
                case Set.WinningTeam.Tie:
                    points = 1;
                    break;
                case Set.WinningTeam.Bravo:
                {
                    points = (decimal) (200F * playerSet.BravoTeam.Score /
                                        ((7 * playerSet.AlphaTeam.Score / playerSet.MatchNum + 4) *
                                         (1 + Math.Pow(10, (double) ((bravoPowerAverage - alphaPowerAverage) / 200))) * 4));

                    break;
                }
                case Set.WinningTeam.Alpha:
                {
                    points = (decimal) (200F * playerSet.AlphaTeam.Score /
                                        ((7 * playerSet.BravoTeam.Score / playerSet.MatchNum + 4) *
                                         (1 + Math.Pow(10, (double) ((alphaPowerAverage - bravoPowerAverage) / 200))) * 4));
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            points /=
                (int) Matchmaker.GetClass(playerSet.AllPlayers.OrderBy(x => x.PowerLevel).First().PowerLevel) -
                (int) Matchmaker.GetClass(playerSet.AllPlayers.OrderByDescending(x => x.PowerLevel).First().PowerLevel)
                + 1;

            //TimePeriod happyPeriod = new TimePeriod(TimeSpan.Parse("20:00"), TimeSpan.Parse("21:00"));
            //if (happyPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault()))
            //{
            //    points *= 2;
            //}

            return points;
        }

        public static async Task<decimal> ReportScores(Set playerSet, bool forgiveLosing = false)
        {
            decimal points = CalculatePoints(playerSet);

            //TimePeriod happyPeriod = new TimePeriod(TimeSpan.Parse("20:00"), TimeSpan.Parse("21:00"));
            //TimePeriod halfPeriod = new TimePeriod(TimeSpan.Parse("1:00"), TimeSpan.Parse("2:00"));

            if (forgiveLosing)
                await MySqlClient.ReportScores(playerSet, points, 0);
            //else if (happyPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault()) ||
            //         halfPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault()))
            //{
            //    await AirTableClient.ReportScores(playerSet, points, points / 2);
            //}
            else
                await MySqlClient.ReportScores(playerSet, points, points);

            return points;
        }
    }
}
