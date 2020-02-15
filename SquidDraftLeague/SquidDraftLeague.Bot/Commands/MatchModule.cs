using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Google.Protobuf;
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
    public class MatchModule
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // TODO this will be eventually moved to the Set class.
        // TODO It's too much of a hassle to update it for the debug stage.
        private static readonly ulong[] OrderedFeedMessages = new ulong[5];

        // NOTE This is where we'll have to implement the different match amounts for cups.
        private const int SET_MATCH_NUMBER = 7;

        public static async Task MoveToMatch(DiscordChannel context, Set set)
        {
            await context.SendMessageAsync("Both teams are drafted! Please wait while roles are being distributed...");

            DiscordRole alphaRole = context.Guild.Roles.First(e => e.Name == $"Alpha ({set.SetNumber})");
            DiscordRole bravoRole = context.Guild.Roles.First(e => e.Name == $"Bravo ({set.SetNumber})");

            foreach (SdlPlayer alphaTeamPlayer in set.AlphaTeam.Players)
            {
                await (await context.Guild.GetMemberAsync(alphaTeamPlayer.DiscordId)).GrantRoleAsync(alphaRole);
            }

            foreach (SdlPlayer bravoTeamPlayer in set.BravoTeam.Players)
            {
                await (await context.Guild.GetMemberAsync(bravoTeamPlayer.DiscordId)).GrantRoleAsync(bravoRole);
            }

            await context.Guild.UpdateRoleAsync(alphaRole, mentionable: true);
            await context.Guild.UpdateRoleAsync(bravoRole, mentionable: true);
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

            DiscordMember hostUser = await context.Guild.GetMemberAsync(selectHostResponse.DiscordId.Value);

            set.Host = set.AllPlayers.First(e => e.DiscordId == hostUser.Id);

            Stage[] mapList = await MySqlClient.GetMapList();
            set.PickStages(mapList);
            Stage selectedStage = set.GetCurrentStage();

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder
            {
                Title = $"Stages for Set #{set.SetNumber}",
                Description = 
                    string.Join("\n", set.Stages.Select(x => $"<:{x.GetModeEmote().Name}:{x.GetModeEmote().Id}> {x.MapName}"))
            };

            await context.Guild.GetChannel(601123765237186560).SendMessageAsync(embed: embed.Build());

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

            await context.SendMessageAsync($"{hostUser.Mention} has been selected as host! The password will be **{hostUser.Discriminator}**. " +
                                           $"Note that if this person is not capable of hosting (due to internet connection etc.) " +
                                           $"it is fine for another person to do so, however keep in mind that situation permitting this person is **first choice** as host.");

            await context.SendMessageAsync("🦑Now then. let the games commence!🐙");

            OrderedFeedMessages[set.SetNumber - 1] = 
                (await context.Guild.GetChannel(666563839646760960)
                    .SendMessageAsync(embed: set.GetFeedEmbedBuilder(context).Build()))
                    .Id;

            DiscordRole setRole = context.Guild.Roles.First(e => e.Name == $"In Set ({set.SetNumber})");

            foreach (SdlPlayer allPlayer in set.AllPlayers)
            {
                await (await context.Guild.GetMemberAsync(allPlayer.DiscordId)).RevokeRoleAsync(setRole);
            }
        }

        [Command("canhost"),
         Description("Toggles whether you wish to be a prioritized candidate to host sets.")]
        public async Task CanHost(CommandContext ctx)
        {
            if (Matchmaker.ToggleCanHost(ctx.User.Id))
            {
                await ctx.RespondAsync("You are now a priority candidate to host during sets.");
            }
            else
            {
                await ctx.RespondAsync("You are no longer a priority candidate to host during sets.");
            }
        }

        [Command("host"),
         Description("Shows information about the host of this set.")]
        public async Task Host(CommandContext ctx)
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == ctx.User.Id));

            if (playerSet == null ||
                (await CommandHelper.ChannelFromSet(playerSet.SetNumber)).Id != ctx.Channel.Id)
            {
                return;
            }

            DiscordMember hostUser = await ctx.Guild.GetMemberAsync(playerSet.Host.DiscordId);

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            builder.AddField(e =>
            {
                e.Name = "Host";
                e.Value = hostUser.Mention;
                e.IsInline = true;
            });

            builder.AddField(e =>
            {
                e.Name = "Generated Password";
                e.Value = hostUser.Discriminator;
                e.IsInline = true;
            });

            await ctx.RespondAsync(embed: builder.Build());
        }

        [Command("dispute"),
         Description("Reports a discrepancy in a set to moderators.")]
        public async Task Dispute(CommandContext ctx)
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == ctx.User.Id));

            DiscordRole modRole = ctx.Guild.Roles.First(e => e.Name == "Moderator");

            if (playerSet == null)
            {
                await ctx.RespondAsync(Resources.ReportErrorResponse);
                return;
            }

            await ctx.RespondAsync($"{modRole.Mention} A dispute has been opened by {ctx.User.Mention}!"+
                                  $"To resolve the error, use `%resolve` and follow the resulting instructions. " +
                                  $"Otherwise, use `%resolve deny` to continue reporting the current score.");

            playerSet.Locked = true;
        }

        [Command("resolve"),
         RequireRole("Moderator")]
        public async Task Resolve(CommandContext ctx, string deny = null, int setNumber = 0)
        {
            Set playerSet;

            if (setNumber == 0)
            {
                playerSet = CommandHelper.SetFromChannel(ctx.Channel.Id);

                if (playerSet == null)
                {
                    await ctx.RespondAsync("Please specify set number or use the correct channel as context.");
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
                    await this.EndMatchAsync(playerSet, ctx);

                    return;
                }

                await ctx.RespondAsync("The report has been resolved by a moderator. This is a final decision. Please continue with the set.");

                Stage selectedRandomStage = playerSet.GetCurrentStage();

                await ctx.RespondAsync(embed: selectedRandomStage
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
            await ctx.RespondAsync("Moderator, use `%overwrite [Team]` to select the winner for this map.", 
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
        public async Task Overwrite(CommandContext ctx, string team, int set = 0)
        {
            Set selectedSet;

            DiscordRole modRole = ctx.Guild.Roles.First(e => e.Name == "Moderator");

            if (set == 0)
            {
                selectedSet = CommandHelper.SetFromChannel(ctx.Channel.Id);

                if (selectedSet == null)
                {
                    await ctx.RespondAsync("Please specify set number or use the correct channel as context.");
                    return;
                }

                if (selectedSet.ResolveMode <= 0)
                {
                    // Force report score by moderator.

                    selectedSet.ReportScore(team);

                    if (selectedSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                        selectedSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                        selectedSet.MatchNum == SET_MATCH_NUMBER)
                    {
                        string winner = selectedSet.AlphaTeam.Score > selectedSet.BravoTeam.Score ? "Alpha" : "Bravo";
                        string loser = selectedSet.AlphaTeam.Score < selectedSet.BravoTeam.Score ? "Alpha" : "Bravo";

                        DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
                        builder.WithTitle("__Results__");
                        builder.AddField(e =>
                        {
                            e.Name = "Score";
                            e.IsInline = true;
                            e.Value = $"{winner} Wins {selectedSet.AlphaTeam.Score} - {selectedSet.BravoTeam.Score}";
                        });

                        await ctx.RespondAsync($"The winner of this set is Team {winner}!", embed: builder.Build());

                        DiscordRole loserRole = loser == "Alpha" ?
                            ctx.Guild.Roles.First(e => e.Name == $"Alpha ({selectedSet.SetNumber})") :
                            ctx.Guild.Roles.First(e => e.Name == $"Bravo ({selectedSet.SetNumber})");

                        await ctx.RespondAsync(
                            $"{loserRole.Mention}, please acknowledge these results by either sending \"confirm\" or \"deny\".");

                        InteractivityModule interactivity = ctx.Client.GetInteractivityModule();

                        DateTime timeoutDateTime = DateTime.Now + TimeSpan.FromMinutes(2);
                        MessageContext replyMessage;
                        try
                        {
                            replyMessage = await interactivity.WaitForMessageAsync(x =>
                            {
                                return ((DiscordMember)x.Author).Roles.Select(e => e.Id).Contains(loserRole.Id);
                            }, TimeSpan.FromMinutes(1));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            Logger.Error(e);
                            throw;
                        }

                        while (true)
                        {
                            if (replyMessage.Message == null)
                            {
                                await ctx.RespondAsync("Time's up! Assuming the losing team has accepted their loss.");

                                await this.EndMatchAsync(selectedSet, ctx);

                                return;
                            }

                            if (replyMessage.Message.Content.ToLower() == "confirm")
                            {
                                await this.EndMatchAsync(selectedSet, ctx);

                                return;
                            }

                            if (replyMessage.Message.Content.ToLower() == "deny")
                            {
                                await ctx.RespondAsync($"{modRole.Mention} issue reported by {replyMessage.Message.Author.Mention}. " +
                                                      $"To resolve the error, use `%resolve` and follow the resulting instructions." +
                                                      $" Otherwise, use `%resolve deny` to continue reporting the current score.");
                                return;
                            }

                            replyMessage = await interactivity.WaitForMessageAsync(x => {
                                return ((DiscordMember)x.Author).Roles.Select(e => e.Id).Contains(loserRole.Id);
                            }, timeoutDateTime - DateTime.Now);
                        }
                    }
                    else
                    {
                        try
                        {
                            selectedSet.MatchNum++;

                            Stage selectedStage = selectedSet.GetCurrentStage();

                            await ctx.RespondAsync(embed: selectedStage
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

                            DiscordMessage feedMessage = (DiscordMessage)await ctx.Guild.GetChannel(666563839646760960).GetMessageAsync(OrderedFeedMessages[selectedSet.SetNumber - 1]);
                            await feedMessage.ModifyAsync(embed: selectedSet.GetFeedEmbedBuilder(ctx.Channel).Build());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    }

                    return;

                    // End force report logic.
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
                    await ctx.RespondAsync("Resolved all issues! All scores will be reported to Airtable.");

                    await this.EndMatchAsync(selectedSet, ctx);
                }
                else
                {
                    await ctx.RespondAsync("Resolved all issues! Teams, continue with your matches.");

                    selectedSet.MatchNum++;

                    Stage selectedStage = selectedSet.Stages[selectedSet.MatchNum - 1];

                    await ctx.RespondAsync(embed: selectedStage
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

                    DiscordMessage feedMessage = await ctx.Guild.GetChannel(666563839646760960).GetMessageAsync(OrderedFeedMessages[selectedSet.SetNumber - 1]);
                    await feedMessage.ModifyAsync(embed: selectedSet.GetFeedEmbedBuilder(ctx.Channel).Build());
                }
            }
            else
            {
                Stage selectedStage = selectedSet.Stages[selectedSet.MatchNum];
                await ctx.RespondAsync("Moderator, use `%overwrite [Team]` to select the winner for this map.",
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
        public async Task Score(CommandContext ctx)
        {
            Set playerSet =
                Matchmaker.Sets.FirstOrDefault(e => e.AllPlayers.Any(f => f.DiscordId == ctx.User.Id));

            DiscordRole modRole = ctx.Guild.Roles.First(e => e.Name == "Moderator");

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
                await ctx.RespondAsync("This set will be locked until a moderator addresses the report. Please wait.");
                return;
            }

            if (playerSet.ResolveMode > 0)
            {
                return;
            }

            if (ctx.Channel.Id != (await CommandHelper.ChannelFromSet(playerSet.SetNumber)).Id)
            {
                return;
            }

            if (!playerSet.AlphaTeam.IsCaptain(ctx.User.Id) && 
                !playerSet.BravoTeam.IsCaptain(ctx.User.Id))
            {
                await ctx.RespondAsync("Only the captain of the losing team can report the score.");
                return;
            }

            string team = playerSet.AlphaTeam.IsCaptain(ctx.User.Id) ? "bravo" : "alpha";

            playerSet.ReportScore(team);

            if (playerSet.AlphaTeam.Score > SET_MATCH_NUMBER / 2 ||
                playerSet.BravoTeam.Score > SET_MATCH_NUMBER / 2 ||
                playerSet.MatchNum == SET_MATCH_NUMBER)
            {
                string winner = playerSet.AlphaTeam.Score > playerSet.BravoTeam.Score ? "Alpha" : "Bravo";
                string loser = playerSet.AlphaTeam.Score < playerSet.BravoTeam.Score ? "Alpha" : "Bravo";

                DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
                builder.WithTitle("__Results__");
                builder.AddField(e =>
                {
                    e.Name = "Score";
                    e.IsInline = true;
                    e.Value = $"{winner} Wins {playerSet.AlphaTeam.Score} - {playerSet.BravoTeam.Score}";
                });

                await ctx.RespondAsync($"The winner of this set is Team {winner}!", embed: builder.Build());

                DiscordRole loserRole = loser == "Alpha" ? 
                    ctx.Guild.Roles.First(e => e.Name == $"Alpha ({playerSet.SetNumber})") : 
                    ctx.Guild.Roles.First(e => e.Name == $"Bravo ({playerSet.SetNumber})");

                await ctx.RespondAsync(
                    $"{loserRole.Mention}, please acknowledge these results by either sending \"confirm\" or \"deny\".");

                InteractivityModule interactivity = ctx.Client.GetInteractivityModule();

                DateTime timeoutDateTime = DateTime.Now + TimeSpan.FromMinutes(2);
                MessageContext replyMessage;
                try
                {
                    replyMessage = await interactivity.WaitForMessageAsync(x =>
                        {
                            return ((DiscordMember) x.Author).Roles.Select(e => e.Id).Contains(loserRole.Id);
                        }, TimeSpan.FromMinutes(1));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Logger.Error(e);
                    throw;
                }

                while (true)
                {
                    if (replyMessage.Message == null)
                    {
                        await ctx.RespondAsync("Time's up! Assuming the losing team has accepted their loss.");

                        await this.EndMatchAsync(playerSet, ctx);

                        return;
                    }

                    if (replyMessage.Message.Content.ToLower() == "confirm")
                    {
                        await this.EndMatchAsync(playerSet, ctx);

                        return;
                    }

                    if (replyMessage.Message.Content.ToLower() == "deny")
                    {
                        await ctx.RespondAsync($"{modRole.Mention} issue reported by {replyMessage.Message.Author.Mention}. " +
                                              $"To resolve the error, use `%resolve` and follow the resulting instructions." +
                                              $" Otherwise, use `%resolve deny` to continue reporting the current score.");
                        return;
                    }

                    replyMessage = await interactivity.WaitForMessageAsync(x => {
                        return ((DiscordMember)x.Author).Roles.Select(e => e.Id).Contains(loserRole.Id);
                    }, timeoutDateTime - DateTime.Now);
                }
            }
            else
            {
                try
                {
                    playerSet.MatchNum++;

                    Stage selectedStage = playerSet.GetCurrentStage();

                    await ctx.RespondAsync(embed: selectedStage
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

                    DiscordMessage feedMessage = (DiscordMessage)await ctx.Guild.GetChannel(666563839646760960).GetMessageAsync(OrderedFeedMessages[playerSet.SetNumber - 1]);
                    await feedMessage.ModifyAsync(embed: playerSet.GetFeedEmbedBuilder(ctx.Channel).Build());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private async Task EndMatchAsync(Set playerSet, CommandContext ctx)
        {
            decimal points = await ReportScores(playerSet);

            //TimePeriod happyPeriod = new TimePeriod(TimeSpan.Parse("20:00"), TimeSpan.Parse("21:00"));
            //TimePeriod halfPeriod = new TimePeriod(TimeSpan.Parse("1:00"), TimeSpan.Parse("2:00"));

            DiscordEmbed setEmbed = playerSet.GetScoreEmbedBuilder(points,
                /*happyPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault()) ||
                halfPeriod.IsWithinPeriod(playerSet.StartTime.GetValueOrDefault())
                    ? points / 2
                    :*/ points).Build();

            await ctx.RespondAsync($"Congratulations Team {playerSet.Winning} on their victory! " +
                                  $"Everyone's power levels have been updated to reflect this match. " +
                                  $"Beginning removal of access to this channel in 30 seconds. " +
                                  $"Rate limiting may cause the full process to take up to two minutes.",
                embed: setEmbed);

            DiscordMessage feedMessage = (DiscordMessage)await ctx.Guild.GetChannel(666563839646760960).GetMessageAsync(OrderedFeedMessages[playerSet.SetNumber - 1]);
            DiscordEmbedBuilder feedEmbedBuilder = playerSet.GetFeedEmbedBuilder(ctx.Channel).WithColor(Color.Green);
            feedEmbedBuilder.Description = $"**{playerSet.Winning}** has won with a score of {playerSet.AlphaTeam.Score}-{playerSet.BravoTeam.Score}!";
            await feedMessage.ModifyAsync(embed: feedEmbedBuilder.Build());

            await Task.Delay(30000);

            List<DiscordRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => ctx.Guild.GetRole(e)).ToList();

            foreach (SdlPlayer playerSetAllPlayer in playerSet.AllPlayers)
            {
                DiscordMember guildUser = await ctx.Guild.GetMemberAsync(playerSetAllPlayer.DiscordId);

                foreach (DiscordRole discordRole in roleRemovalList.Where(e =>
                    ctx.Guild.GetMemberAsync(guildUser.Id).Result.Roles
                        .Any(f => e.Id == f.Id)))
                {
                    await guildUser.RevokeRoleAsync(discordRole);
                }
            }

            playerSet.Close();

            DiscordRole classOneRole = ctx.Guild.GetRole(600770643075661824);
            DiscordRole classTwoRole = ctx.Guild.GetRole(600770814521901076);
            DiscordRole classThreeRole = ctx.Guild.GetRole(600770862307606542);
            DiscordRole classFourRole = ctx.Guild.GetRole(600770905282576406);

            DiscordRole[] allClassRoles = { classOneRole, classTwoRole, classThreeRole, classFourRole };

            string optOutDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Opt Out")).FullName;

            foreach (SdlPlayer sdlPlayer in await MySqlClient.RetrieveAllSdlPlayers())
            {
                try
                {
                    DiscordMember sdlGuildUser = await ctx.Guild.GetMemberAsync(sdlPlayer.DiscordId);

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

                                await sdlGuildUser.GrantRoleAsync(classOneRole);
                            }
                            break;
                        case SdlClass.Two:
                            if (sdlGuildUser.Roles.All(e => e.Id != classTwoRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.GrantRoleAsync(classTwoRole);
                            }
                            break;
                        case SdlClass.Three:
                            if (sdlGuildUser.Roles.All(e => e.Id != classThreeRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.GrantRoleAsync(classThreeRole);
                            }
                            break;
                        case SdlClass.Four:
                            if (sdlGuildUser.Roles.All(e => e.Id != classFourRole.Id))
                            {
                                await sdlGuildUser.RemoveRolesAsync(allClassRoles.Where(e =>
                                    sdlGuildUser.Roles.Any(f => f.Id == e.Id)));

                                await sdlGuildUser.GrantRoleAsync(classFourRole);
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
            if (ctx.Channel.Id == 0 /*CommandHelper.ChannelFromSet(playerSet.SetNumber).Id*/)
            {
                /*IEnumerable<IMessage> messages = await ctx.Channel.GetMessagesAsync(1000 + 1).FlattenAsync();

                await ((ITextChannel) ctx.Channel).DeleteMessagesAsync(messages);

                DiscordMessage reply =
                    await ctx.RespondAsync(
                        "Cleared the channel of 1000 messages. This message will be deleted in 10 seconds as well.");

                await Task.Delay(10000);

                await reply.DeleteAsync();*/
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
            
            if (forgiveLosing)
            {
                DiscordMessage feedMessage = (DiscordMessage) await (await Program.Client.GetGuildAsync(570743985530863649))
                    .GetChannel(666563839646760960).GetMessageAsync(OrderedFeedMessages[playerSet.SetNumber - 1]);
                await feedMessage.ModifyAsync(embed: playerSet.GetFeedEmbedBuilder(null)
                    .WithDescription("The set ended unnaturally.").Build());
            }

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
