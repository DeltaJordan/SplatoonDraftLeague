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
using SquidDraftLeague.AirTable;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Draft Cup")]
    public class TournamentModule : InteractiveBase
    {
        private static readonly List<ulong> AllAvailablePlayers = new List<ulong>();
        private static readonly List<ulong> CurrentCaptains = new List<ulong>();
        private static readonly Dictionary<ulong, List<ulong>> Teams = new Dictionary<ulong, List<ulong>>();
        private static readonly List<int> RequiredCaptains = new List<int>();
        private static int currentCaptainIndex;
        private static bool isReverse = true;

        [Command("signUp")]
        public async Task SignUp()
        {
            if (!(this.Context.User is SocketGuildUser user))
            {
                return;
            }

            IDMChannel dmChannel = await this.Context.User.GetOrCreateDMChannelAsync();

            if (user.Roles.All(e => !string.Equals(e.Name, "Player", StringComparison.InvariantCultureIgnoreCase)))
            {
                await dmChannel.SendMessageAsync(
                    "You must be registered to the bot to register for SDL Draft Cup. Use %regapply to do so now.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
            string codesFile = Path.Combine(tournamentDirectory, "codes.json");
            string signUpFile = Path.Combine(tournamentDirectory, "signups.json");

            List<string> codes;

            if (File.Exists(codesFile))
            {
                codes = JsonConvert.DeserializeObject<List<string>>(await File.ReadAllTextAsync(codesFile));

                if (!codes.Any())
                {
                    await this.ReplyAsync("<@&572539082039885839> There are no more available battlefy codes!");
                    return;
                }
            }
            else
                return;

            if ((now.Day < 28 && now.Month == 7) || (now.Day == 28 && now.Month == 7 && now.Hour < 16))
            {
                List<ulong> signUpIds = new List<ulong>();

                if (File.Exists(signUpFile))
                {
                    signUpIds = JsonConvert.DeserializeObject<List<ulong>>(await File.ReadAllTextAsync(signUpFile));
                }

                if (signUpIds.Contains(this.Context.User.Id))
                {
                    await this.ReplyAsync("You are already signed up!");
                    return;
                }

                string code = codes.First();

                await dmChannel.SendMessageAsync(
                    $"Your battlefy code is {code}. " +
                    $"Sign ups are located at https://battlefy.com/splatoon-draft-league/draft-cup-1/5d22d41ea0c700585fec7baf/info. " +
                    $"Enter your join code where the website says \"Enter Join Code\".");

                codes.Remove(code);

                await File.WriteAllTextAsync(codesFile, JsonConvert.SerializeObject(codes));

                signUpIds.Add(this.Context.User.Id);

                await File.WriteAllTextAsync(signUpFile, JsonConvert.SerializeObject(signUpIds));
            }
            else
            {
                await this.ReplyAsync("Sign ups are currently closed.");
            }
        }

        [Command("checkIn")]
        public async Task CheckIn()
        {
            IDMChannel userDmChannel = await this.Context.User.GetOrCreateDMChannelAsync();
            bool enabled = false;

            string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
            string checkInFile = Path.Combine(tournamentDirectory, "checkIn.json");
            string signUpFile = Path.Combine(tournamentDirectory, "signups.json");
            string checkedInPlayersFile = Path.Combine(tournamentDirectory, "checkedIn.json");

            if (File.Exists(checkInFile))
            {
                enabled = JsonConvert.DeserializeObject<bool>(await File.ReadAllTextAsync(checkInFile));
            }

            if (!enabled)
                return;

            List<ulong> checkedInPlayerIds = new List<ulong>();

            if (File.Exists(checkedInPlayersFile))
            {
                checkedInPlayerIds =
                    JsonConvert.DeserializeObject<List<ulong>>(await File.ReadAllTextAsync(checkedInPlayersFile));

                if (checkedInPlayerIds.Contains(this.Context.User.Id))
                {
                    await userDmChannel.SendMessageAsync("You have already checked in.");
                    return;
                }
            }

            List<ulong> signUpIds = JsonConvert.DeserializeObject<List<ulong>>(await File.ReadAllTextAsync(signUpFile));

            if (!signUpIds.Contains(this.Context.User.Id))
            {
                await userDmChannel.SendMessageAsync("You have not signed up for the draft cup.");
                return;
            }

            IRole checkInRole = this.Context.Guild.GetRole(604731261033906225);
            await ((IGuildUser) this.Context.User).AddRoleAsync(checkInRole);

            checkedInPlayerIds.Add(this.Context.User.Id);

            await File.WriteAllTextAsync(checkedInPlayersFile, JsonConvert.SerializeObject(checkedInPlayerIds));

            await userDmChannel.SendMessageAsync("You have successfully checked in to the draft cup! Thanks again for participating.");
        }

        [Command("toggleCheckIn"), Alias("tgi", "togCheckIn"), RequireRole("Moderator")]
        public async Task ToggleCheckIn()
        {
            bool enabled = false;

            string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
            string checkInFile = Path.Combine(tournamentDirectory, "checkIn.json");
            if (File.Exists(checkInFile))
            {
                enabled = JsonConvert.DeserializeObject<bool>(await File.ReadAllTextAsync(checkInFile));
            }

            enabled = !enabled;

            await File.WriteAllTextAsync(checkInFile, JsonConvert.SerializeObject(enabled));

            await this.ReplyAsync($"Check-ins are now {(enabled ? "active" : "closed")}.");
        }

        [Command("draftFor"), RequireRole("Moderator")]
        public async Task DraftFor()
        {
            try
            {
                await this.DraftPick(
                    this.Context.Guild.GetUser(AllAvailablePlayers[Globals.Random.Next(0, AllAvailablePlayers.Count - 1)]));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("draft"), RequireChannel(603361858522578954)]
        public async Task Draft(IGuildUser pickedUser)
        {
            if (!(this.Context.User is IGuildUser user))
                return;

            if (user.Id != CurrentCaptains[currentCaptainIndex])
            {
                await this.ReplyAsync("It is not your turn to draft!");
                return;
            }

            if (!AllAvailablePlayers.Contains(pickedUser.Id))
            {
                await this.ReplyAsync("This person is not available to be drafted!");
                return;
            }

            try
            {
                await this.DraftPick(pickedUser);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async Task DraftPick(IGuildUser pickedUser)
        {
            IGuildUser user = this.Context.Guild.GetUser(CurrentCaptains[currentCaptainIndex]);

            AllAvailablePlayers.Remove(pickedUser.Id);
            Teams[user.Id].Add(pickedUser.Id);

            if (isReverse)
            {
                if (currentCaptainIndex - 1 < 0)
                {
                    isReverse = false;
                }
                else
                {
                    currentCaptainIndex--;
                }
            }
            else
            {
                if (currentCaptainIndex + 1 == CurrentCaptains.Count)
                {
                    isReverse = true;
                }
                else
                {
                    currentCaptainIndex++;
                }
            }

            await this.ReplyAsync($"{pickedUser.Mention} was added to {user.Mention}'s team.");

            if (Teams.Any(x => x.Value.Count < 4))
                await this.ReplyAsync($"It is now {CurrentCaptains[currentCaptainIndex].ToUserMention()}'s turn to pick.");
            else
            {
                RequiredCaptains.RemoveAt(0);

                int neededCaptains = RequiredCaptains.FirstOrDefault();

                if (neededCaptains > 0)
                {
                    isReverse = true;
                    currentCaptainIndex = neededCaptains - 1;
                    CurrentCaptains.Clear();

                    int skipAmount = 0;

                    IUserMessage messageToDelete = await this.ReplyAsync("Retrieving available players please wait...");

                    SdlPlayer[] allSdlPlayers = await AirTableClient.RetrieveAllSdlPlayers();

                    for (int i = 0; i < RequiredCaptains[0]; i++)
                    {
                        while (!await AirTableClient.CheckHasPlayedSet(allSdlPlayers.First(x =>
                            x.DiscordId == AllAvailablePlayers[i + skipAmount])))
                        {
                            skipAmount++;
                        }

                        CurrentCaptains.Add(AllAvailablePlayers[i + skipAmount]);
                    }

                    string captainMentions =
                        string.Join(" ", CurrentCaptains.Select(x => this.Context.Guild.GetUser(x).Mention));

                    AllAvailablePlayers.RemoveAll(x => CurrentCaptains.Contains(x));

                    foreach (ulong currentCaptain in CurrentCaptains)
                    {
                        if (!Teams.ContainsKey(currentCaptain))
                            Teams.Add(currentCaptain, new List<ulong> { currentCaptain });
                    }

                    await this.ReplyAsync($"The next captains will be {captainMentions}.");

                    await this.ReplyAsync(
                        $"{this.Context.Guild.GetUser(CurrentCaptains[currentCaptainIndex]).Mention} will pick first by using `%draft [player]`.");

                    foreach (EmbedBuilder groupEmbedBuilder in this.GetGroupEmbedBuilders("Available Players",
                        allSdlPlayers.Where(x => AllAvailablePlayers.Contains(x.DiscordId))))
                    {
                        await this.ReplyAsync(embed: groupEmbedBuilder.Build());
                    }

                    await messageToDelete.DeleteAsync();
                }
                else
                {
                    string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
                    string teamDirectory = Directory.CreateDirectory(Path.Combine(tournamentDirectory, "Teams")).FullName;

                    foreach (KeyValuePair<ulong, List<ulong>> team in Teams)
                    {
                        await File.WriteAllTextAsync(Path.Combine(teamDirectory, $"{team.Key}.json"),
                            JsonConvert.SerializeObject(team));
                    }

                    await this.ReplyAsync(
                        $"All teams ({Teams.Count} of them) have been successfully drafted! " +
                        $"{string.Join('\n', AllAvailablePlayers.Select(x => x.ToUserMention()))} " + 
                        $"Unfortunately, all teams have been filled. " +
                        $"However don't fret, you may opt into subbing for a team member if needed.");
                }
            }
        }

        [Command("teams"), RequireRole("Moderator")]
        public async Task GetTeams()
        {
            try
            {
                string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
                string teamDirectory = Directory.CreateDirectory(Path.Combine(tournamentDirectory, "Teams")).FullName;

                EmbedBuilder builder = new EmbedBuilder
                {
                    Title = "Currently Registered Teams"
                };

                foreach (string file in Directory.EnumerateFiles(teamDirectory))
                {
                    (ulong captain, List<ulong> team) =
                        JsonConvert.DeserializeObject<KeyValuePair<ulong, List<ulong>>>(await File.ReadAllTextAsync(file));

                    builder.AddField(x =>
                    {
                        x.Name = $"{this.Context.Guild.GetUser(captain).Username}'s Team:";
                        x.Value = string.Join("\n", team.Select(y => y.ToUserMention()));
                        x.IsInline = false;
                    });

                    if (builder.Fields.Count == EmbedBuilder.MaxFieldCount)
                    {
                        await this.ReplyAsync(embed: builder.Build());

                        builder = new EmbedBuilder
                        {
                            Title = "Currently Registered Teams (cont.)"
                        };
                    }
                }

                if (builder.Fields.Count > 0)
                {
                    await this.ReplyAsync(embed: builder.Build());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("startDraft"), RequireRole("Moderator"), RequireChannel(603361858522578954)]
        public async Task StartDraft()
        {
            try
            {
                await this.ReplyAsync("Putting everything together, this may take a bit...");

                string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
                string checkedInPlayersFile = Path.Combine(tournamentDirectory, "checkedIn.json");

                // TODO Roles
                IRole captainRole;
                IRole playerRole;

                List<ulong> checkedInIds =
                    JsonConvert.DeserializeObject<List<ulong>>(await File.ReadAllTextAsync(checkedInPlayersFile));

                SdlPlayer[] allSdlPlayers = await AirTableClient.RetrieveAllSdlPlayers();

                AllAvailablePlayers.AddRange(allSdlPlayers
                    .Where(x => checkedInIds.Contains(x.DiscordId))
                    .OrderByDescending(x => x.PowerLevel)
                    .Select(x => x.DiscordId));

                RequiredCaptains.AddRange(this.GetRoundCaptainCounts());

                CurrentCaptains.Clear();

                int skipAmount = 0;

                for (int i = 0; i < RequiredCaptains[0]; i++)
                {
                    while (!await AirTableClient.CheckHasPlayedSet(allSdlPlayers.First(x =>
                        x.DiscordId == AllAvailablePlayers[i + skipAmount])))
                    {
                        skipAmount++;
                    }

                    CurrentCaptains.Add(AllAvailablePlayers[i + skipAmount]);
                }

                string captainMentions =
                    string.Join(" ", CurrentCaptains.Select(x => this.Context.Guild.GetUser(x).Mention));

                AllAvailablePlayers.RemoveAll(x => CurrentCaptains.Contains(x));

                foreach (ulong currentCaptain in CurrentCaptains)
                {
                    Teams.Add(currentCaptain, new List<ulong>{currentCaptain});
                }

                await this.ReplyAsync($"The first captains will be {captainMentions}.");

                currentCaptainIndex = RequiredCaptains[0] - 1;

                await this.ReplyAsync(
                    $"{this.Context.Guild.GetUser(CurrentCaptains.Last()).Mention} will pick first by using `%draft [@player]`.");

                IUserMessage messageToDelete = await this.ReplyAsync("Retrieving available players please wait...");

                foreach (EmbedBuilder groupEmbedBuilder in this.GetGroupEmbedBuilders("Available Players",
                    allSdlPlayers.Where(x => AllAvailablePlayers.Contains(x.DiscordId))))
                {
                    await this.ReplyAsync(embed: groupEmbedBuilder.Build());
                }

                await messageToDelete.DeleteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("addCodes"),
         RequireRole("Moderator")]
        public async Task AddCodes(params string[] codes)
        {
            if (codes.Any(e => e.Length != 7))
            {
                await this.ReplyAsync("Invalid code detected! All codes should be 7 in length.");
                return;
            }

            string tournamentDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Tournament")).FullName;
            string codesFile = Path.Combine(tournamentDirectory, "codes.json");
            List<string> codesList = new List<string>();

            if (File.Exists(codesFile))
            {
                codesList = JsonConvert.DeserializeObject<List<string>>(await File.ReadAllTextAsync(codesFile));
            }

            codesList.AddRange(codes);

            await File.WriteAllTextAsync(codesFile, JsonConvert.SerializeObject(codesList));

            await this.ReplyAsync($"Added {codes.Length} codes. There are now {codesList.Count} available codes.");
        }

        private List<int> GetRoundCaptainCounts()
        {
            int playerCount = AllAvailablePlayers.Count;

            int roundCount;
            for (roundCount = 4; roundCount > 1; roundCount--)
            {
                if (playerCount / (roundCount * 4) >= 4)
                {
                    break;
                }
            }

            int minimumRequired = playerCount / (4 * roundCount);
            int remainingPlayers = playerCount - minimumRequired * 4 * roundCount;
            int distributeNum = (minimumRequired * 4 + remainingPlayers) / 4;

            List<int> roundCaptainAmounts = new List<int> { distributeNum };

            for (int i = 1; i < roundCount; i++)
            {
                roundCaptainAmounts.Add(minimumRequired);
            }

            if (roundCaptainAmounts.Count > 1)
            {
                while (roundCaptainAmounts[0] - 1 >= roundCaptainAmounts[1])
                {
                    for (int i = 1; i < 4; i++)
                    {
                        roundCaptainAmounts[0] -= 1;
                        roundCaptainAmounts[i] += 1;

                        if (roundCaptainAmounts[0] - 1 < roundCaptainAmounts[1])
                        {
                            break;
                        }
                    }
                }
            }

            return roundCaptainAmounts;
        }

        private EmbedBuilder[] GetGroupEmbedBuilders(string title, IEnumerable<SdlPlayer> group)
        {
            List<string> users = new List<string>();

            foreach (SdlPlayer sdlPlayer in group)
            {
                string displayText = this.Context.Guild.GetUser(sdlPlayer.DiscordId).Mention;

                displayText += $" [{sdlPlayer.PowerLevel:0.0}]";

                if (!string.IsNullOrWhiteSpace(sdlPlayer.Role))
                {
                    displayText += $" [{sdlPlayer.Role}]";
                }

                displayText += "\n";

                users.Add(displayText);
            }

            List<string> userLines = new List<string>();
            string currentLine = string.Empty;

            foreach (string userLine in users)
            {
                if ((currentLine + userLine).Length > EmbedBuilder.MaxDescriptionLength)
                {
                    userLines.Add(currentLine);
                    currentLine = string.Empty;
                }

                currentLine += userLine;
            }

            userLines.Add(currentLine);

            List<EmbedBuilder> pagedBuilders = new List<EmbedBuilder>();

            for (int i = 0; i < userLines.Count; i++)
            {
                string userLine = userLines[i];

                EmbedBuilder builder = new EmbedBuilder
                {
                    Title = title + (i == 0 ? "" : "(cont.)"),
                    Description = userLine
                };

                builder.WithFooter(x => x.Text = $"Page {i + 1}");

                pagedBuilders.Add(builder);
            }

            return pagedBuilders.ToArray();
        }
    }
}
