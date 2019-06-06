using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Bot.Queuing;
using SquidDraftLeague.Bot.Queuing.Data;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Match")]
    public class MatchModule : InteractiveBase<SocketCommandContext>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // NOTE This is where we'll have to implement the different match amounts for cups.
        const int SET_MATCH_NUMBER = 7;

        public static async Task MoveToMatch(ITextChannel context, Set set)
        {
            await context.SendMessageAsync("Both teams are drafted! Please wait while roles are being distributed...");

            IRole alphaRole = context.Guild.Roles.First(e => e.Name == $"Alpha ({set.SetNumber})");
            IRole bravoRole = context.Guild.Roles.First(e => e.Name == $"Bravo ({set.SetNumber})");

            foreach (SdlPlayer alphaTeamPlayer in set.AlphaTeam.Players)
            {
                await alphaTeamPlayer.DiscordId.GetGuildUser(null).AddRoleAsync(alphaRole);
            }

            foreach (SdlPlayer bravoTeamPlayer in set.BravoTeam.Players)
            {
                await bravoTeamPlayer.DiscordId.GetGuildUser(null).AddRoleAsync(bravoRole);
            }

            await alphaRole.ModifyAsync(e => e.Mentionable = true);
            await bravoRole.ModifyAsync(e => e.Mentionable = true);
            await context.SendMessageAsync($"{alphaRole.Mention} {bravoRole.Mention} To begin, please join your respective voice channels.\n" +
                                                   $"After everyone is situated, please create and join a Private Battle and have the host select the map and mode chosen after this message. " +
                                                   $"Once the match is over, use %score to report the winning team like so:\n" +
                                                   $"`%score Alpha` or `%score Bravo`");

            Stage[] stages = await AirTableClient.GetMapList();

            Stage selectedStage = stages[Globals.Random.Next(0, stages.Length - 1)];

            set.PlayedStages.Add(selectedStage);

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

            set.MatchNum = 1;

            await context.SendMessageAsync("Good luck both teams on their matches! :)");
        }

        [Command("report")]
        public async Task Report()
        {
            Set playerSet =
                SetModule.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId.GetGuildUser(this.Context).Id == this.Context.User.Id));

            IRole modRole = this.Context.Guild.Roles.First(e => e.Name == "Moderator");

            if (playerSet == null)
            {
                return;
            }

            await this.ReplyAsync($"{modRole.Mention} A dispute has been opened by {this.Context.User.Mention}!");

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
                playerSet = SetModule.Sets[setNumber - 1];
            }

            playerSet.Locked = false;

            if (deny == "deny")
            {
                if (playerSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                    playerSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                    playerSet.MatchNum == SET_MATCH_NUMBER)
                {
                    string winner = playerSet.AlphaTeam.Score > playerSet.BravoTeam.Score ? "Alpha" : "Bravo";
                    await this.EndMatchAsync(playerSet, winner);

                    return;
                }

                await this.ReplyAsync("The report has been resolved by a moderator. This is a final decision. Please continue with the set.");

                playerSet.MatchNum++;

                List<Stage> stages = (await AirTableClient.GetMapList())
                    .Where(e => !(playerSet.PlayedStages.Any(f => f.MapName == e.MapName) && playerSet.PlayedStages.Any(f => f.Mode == e.Mode)))
                    .ToList();

                Stage selectedRandomStage = stages[Globals.Random.Next(0, stages.Count - 1)];

                playerSet.PlayedStages.Add(selectedRandomStage);

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

            playerSet.ResolveMode = playerSet.MatchNum;

            playerSet.MatchNum = 0;
            playerSet.AlphaTeam.OrderedMatchResults.Clear();
            playerSet.BravoTeam.OrderedMatchResults.Clear();

            Stage selectedStage = playerSet.PlayedStages[0];
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
                selectedSet = SetModule.Sets[set - 1];
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

                    string winner = selectedSet.AlphaTeam.Score > selectedSet.BravoTeam.Score ? "Alpha" : "Bravo";
                    await this.EndMatchAsync(selectedSet, winner);
                }
                else
                {
                    await this.ReplyAsync("Resolved all issues! Teams, continue with your matches.");

                    List<Stage> stages = (await AirTableClient.GetMapList())
                        .Where(e => !(selectedSet.PlayedStages.Any(f => f.MapName == e.MapName) && selectedSet.PlayedStages.Any(f => f.Mode == e.Mode)))
                        .ToList();

                    Stage selectedStage = stages[Globals.Random.Next(0, stages.Count - 1)];

                    selectedSet.PlayedStages.Add(selectedStage);

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
                Stage selectedStage = selectedSet.PlayedStages[selectedSet.MatchNum - 1];
                await this.ReplyAsync("Moderator, use `%overwrite [Team]` to select the winner for this map.",
                    embed: selectedStage.GetEmbedBuilder($"Match {selectedSet.MatchNum} of 7: {selectedStage.MapName}")
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
        public async Task Score(string team)
        {
            Set playerSet =
                SetModule.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId.GetGuildUser(this.Context).Id == this.Context.User.Id));

            IRole modRole = this.Context.Guild.Roles.First(e => e.Name == "Moderator");

            if (playerSet == null)
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

            team = team.ToLower();

            if (team != "bravo" && team != "alpha")
            {
                await this.ReplyAsync("Please use the terms \"alpha\" or \"bravo\" to indicate the winning team.");
                return;
            }

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
                    replyMessage = await this.NextMessageAsync(false, true, TimeSpan.FromMinutes(2));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Logger.Error(e);
                    throw;
                }

                while (true)
                {
                    Console.WriteLine(replyMessage);

                    if (replyMessage == null)
                    {
                        await this.ReplyAsync("Times up! Assuming the losing team has accepted their loss.");

                        await this.EndMatchAsync(playerSet, winner);

                        return;
                    }

                    SocketGuildUser authorGuildUser = (SocketGuildUser) replyMessage.Author;

                    if (!authorGuildUser.Roles.Select(e => e.Id).Contains(loserRole.Id))
                    {
                        if (DateTime.Now > timeoutDateTime)
                        {
                            await this.EndMatchAsync(playerSet, winner);

                            return;
                        }

                        replyMessage = await this.NextMessageAsync(false, true, timeoutDateTime - DateTime.Now);
                        continue;
                    }

                    if (replyMessage.Content.ToLower() == "confirm")
                    {
                        await this.EndMatchAsync(playerSet, winner);

                        return;
                    }

                    if (replyMessage.Content.ToLower() == "deny")
                    {
                        await this.ReplyAsync($"{modRole.Mention} issue reported by {replyMessage.Author.Mention}. " +
                                              $"To resolve the error, use `%resolve` and follow the resulting instructions." +
                                              $"Otherwise, use `%resolve deny` to continue reporting the current score.");
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

                    List<Stage> stages = (await AirTableClient.GetMapList())
                        .Where(e => !(playerSet.PlayedStages.Any(f => f.MapName == e.MapName) && playerSet.PlayedStages.Any(f => f.Mode == e.Mode)))
                        .ToList();

                    Stage selectedStage = stages[Globals.Random.Next(0, stages.Count - 1)];

                    playerSet.PlayedStages.Add(selectedStage);

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

        private async Task EndMatchAsync(Set playerSet, string winner)
        {
            await ReportScores(playerSet, winner);

            await this.ReplyAsync($"Congratulations Team {winner} on their victory! " +
                                  $"Everyone's power levels have been updated to reflect this match. " +
                                  $"Use `%profile` to check that. For now, you have 30 seconds to converse in this channel before access is removed.");

            await Task.Delay(30000);

            List<SocketRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => this.Context.Guild.GetRole(e)).ToList();

            foreach (SdlPlayer playerSetAllPlayer in playerSet.AllPlayers)
            {
                await playerSetAllPlayer.DiscordId.GetGuildUser(this.Context).RemoveRolesAsync(roleRemovalList.Where(e =>
                    this.Context.Guild.GetUser(playerSetAllPlayer.DiscordId.GetGuildUser(this.Context).Id).Roles
                        .Any(f => e.Id == f.Id)));
            }

            playerSet.Close();
        }

        private static async Task ReportScores(Set playerSet, string winner)
        {
            double alphaPowerAverage = playerSet.AlphaTeam.Players.Select(e => e.PowerLevel).Average();
            double bravoPowerAverage = playerSet.BravoTeam.Players.Select(e => e.PowerLevel).Average();

            double powerDifference;
            if (winner == "Bravo")
            {
                powerDifference = 50 * ((Math.Pow(bravoPowerAverage, 12) + Math.Pow(alphaPowerAverage, 12)) /
                                        (SET_MATCH_NUMBER * Math.Pow(bravoPowerAverage, 12)));
            }
            else
            {
                powerDifference = 50 * ((Math.Pow(alphaPowerAverage, 12) + Math.Pow(bravoPowerAverage, 12)) /
                      (SET_MATCH_NUMBER * Math.Pow(alphaPowerAverage, 12)));
            }

            await AirTableClient.ReportScores(playerSet, powerDifference, powerDifference);
        }
    }
}
