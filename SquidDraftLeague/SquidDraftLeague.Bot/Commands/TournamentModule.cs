using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Newtonsoft.Json;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Draft Cup")]
    public class TournamentModule : InteractiveBase
    {
        [Command("signUp")]
        public async Task SignUp()
        {
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

                IDMChannel dmChannel = await this.Context.User.GetOrCreateDMChannelAsync();

                string code = codes.First();

                await dmChannel.SendMessageAsync(
                    $"Your battlefy code is {code}. " +
                    $"Sign ups are located at https://battlefy.com/splatoon-draft-league/draft-cup-1/5d22d41ea0c700585fec7baf/info");

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
    }
}
