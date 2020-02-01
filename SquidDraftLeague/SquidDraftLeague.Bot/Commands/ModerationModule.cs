using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Moderation")]
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        [Command("migrate"), RequireOwner]
        public async Task Migrate()
        {
            try
            {
                SdlPlayer[] players =
                    JsonConvert.DeserializeObject<SdlPlayer[]>(
                        File.ReadAllText(Path.Combine(Globals.AppPath, "players.json")));

                foreach (SdlPlayer sdlPlayer in players)
                {
                    if (sdlPlayer == null)
                        continue;

                    string nickname = sdlPlayer.Nickname;

                    if (string.IsNullOrWhiteSpace(nickname))
                    {
                        nickname = this.Context.Guild.Users.Any(x => x.Id == sdlPlayer.DiscordId)
                            ? this.Context.Guild.GetUser(sdlPlayer.DiscordId).Username
                            : "!NONE!";
                    }

                    await MySqlClient.RegisterPlayer(sdlPlayer.DiscordId, (double) sdlPlayer.PowerLevel, nickname);

                    Console.WriteLine($"{sdlPlayer.Nickname} completed.");
                }

                await this.ReplyAsync("Complete.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /*[Command("limit"),
         Summary("Very complicated command to modify what commands can be used where and when. " +
                 "This command is used similarly to command prompt commands with arguments. " +
                 "This means that all arguments with spaces need to be wrapped in quotes. " +
                 "This command also assumes that the command is to be allowed according " +
                 "to the arguments unless specified otherwise."),
         RequireRole("Moderator")]
        public async Task Limit(params string[] args)
        {
            try
            {
                List<KeyValuePair<string, List<string>>> argumentPairs = new List<KeyValuePair<string, List<string>>>();

                bool clear = false;
                bool inverse = false;
                bool all = false;

                List<string> names = new List<string>();

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith("-"))
                    {
                        if (string.Equals(args[i].TrimStart('-'), "all", StringComparison.InvariantCultureIgnoreCase))
                        {
                            all = true;
                            names.Add("all");
                            continue;
                        }

                        if (string.Equals(args[i].TrimStart('-'), "deny", StringComparison.InvariantCultureIgnoreCase))
                        {
                            inverse = true;
                            continue;
                        }

                        List<string> arguments = new List<string>();

                        string argumentIndicator = args[i].TrimStart('-');

                        while (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            arguments.Add(args[i + 1]);

                            i++;
                        }

                        argumentPairs.Add(new KeyValuePair<string, List<string>>(argumentIndicator, arguments));
                    }
                    else
                    {
                        await this.ReplyAsync($"Invalid argument {args[i]}!");
                        return;
                    }
                }

                List<ILimitation> limitations = new List<ILimitation>();

                foreach ((string indicator, List<string> arguments) in argumentPairs)
                {
                    switch (indicator.ToLower())
                    {
                        case "clear":
                            clear = true;
                            break;
                        case "now":
                            limitations.Add(new UnconditionalLimitation());
                            break;
                        case "c":
                        case "command":
                        case "group":
                        case "g":
                            if (all)
                            {
                                continue;
                            }

                            names.AddRange(arguments);
                            break;
                        case "t":
                            List<TimePeriod> timePeriods = new List<TimePeriod>();
                            for (int i = 0; i < arguments.Count; i += 2)
                            {
                                timePeriods.Add(new TimePeriod(TimeSpan.Parse(arguments[i]), TimeSpan.Parse(arguments[i + 1])));
                            }

                            limitations.Add(new TimeLimitation(timePeriods.ToArray()));
                            break;
                        case "ch":
                        case "channel":
                            if (!arguments.Any())
                            {
                                limitations.Add(new ChannelLimitation(this.Context.Channel.Id));
                            }
                            else
                            {
                                foreach (string argument in arguments)
                                {
                                    if (ulong.TryParse(argument, out ulong result))
                                    {
                                        limitations.Add(new ChannelLimitation(result));
                                    }
                                    else
                                    {
                                        await this.ReplyAsync($"Unable to parse UInt64 {argument}. Channel must be the number indicator of a Discord channel aka the snowflake id.");
                                        return;
                                    }
                                }
                            }
                            break;
                        case "r":
                        case "role":
                            foreach (string argument in arguments)
                            {
                                limitations.Add(new RoleLimitation(argument));
                            }
                            break;
                    }
                }

                if (!names.Any())
                {
                    await this.ReplyAsync("You must specify a command name, group name, or use `--all`.");
                    return;
                }

                foreach (ILimitation limitation in limitations)
                {
                    limitation.Inverse = inverse;
                }

                string limitDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Limiters")).FullName;

                foreach (string name in names)
                {
                    CommandLimiter commandLimiter;

                    if (File.Exists(Path.Combine(limitDirectory, $"{name}")))
                    {
                        if (clear)
                        {
                            File.Delete(Path.Combine(limitDirectory, $"{name}"));
                            continue;
                        }

                        commandLimiter = JsonConvert.DeserializeObject<CommandLimiter>(
                            await File.ReadAllTextAsync(Path.Combine(limitDirectory, $"{name}")),
                            new JsonSerializerSettings
                            {
                                TypeNameHandling = TypeNameHandling.Auto
                            });

                        commandLimiter.Limitations = commandLimiter.Limitations.Concat(limitations).ToArray();
                    }
                    else
                    {
                        if (clear)
                        {
                            continue;
                        }

                        commandLimiter = new CommandLimiter
                        {
                            Name = name,
                            Limitations = limitations.ToArray()
                        };
                    }

                    await File.WriteAllTextAsync(Path.Combine(limitDirectory, $"{name}"),
                        JsonConvert.SerializeObject(commandLimiter, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.Auto,
                            Formatting = Formatting.Indented
                        }));
                }

                await this.ReplyAsync($"The following limitations will now be enforced for {string.Join(", ", names)}:\n" +
                                      string.Join("\n", limitations.Select(GetLimitationInfo)));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static string GetLimitationInfo(ILimitation limitation)
        {
            if (limitation is UnconditionalLimitation unconditionalLimitation)
            {
                return "Will be " +
                    (unconditionalLimitation.Inverse ? "denied" : "allowed") +
                    " unconditionally. Note that any other limitations listed will be completely ignored.";
            }

            if (limitation is ChannelLimitation channelLimitation)
            {
                if (channelLimitation.Inverse)
                {
                    return $"Will be allowed in all channels but the channel with id {channelLimitation.ChannelId}.";
                }

                return $"Will be allowed only in the channel with id {channelLimitation.ChannelId}";
            }

            if (limitation is RoleLimitation roleLimitation)
            {
                if (roleLimitation.Inverse)
                {
                    return "Will be only usable by users without the role " + roleLimitation.RoleName;
                }

                return "Will be only usable by users with the role " + roleLimitation.RoleName;
            }

            if (limitation is TimeLimitation timeLimitation)
            {
                if (timeLimitation.Inverse)
                {
                    return "Will be only usable outside the following times (UTC): " + 
                           string.Join(", ", timeLimitation.TimePeriods.Select(e => $"{e.Start:hh\\:mm} - {e.End:hh\\:mm}"));
                }

                return "Will be only usable during the following times (UTC): " + 
                       string.Join(", ", timeLimitation.TimePeriods.Select(e => $"{e.Start:hh\\:mm} - {e.End:hh\\:mm}"));
            }

            return "Unknown limitation " + limitation.GetType().FullName;
        }*/
    }
}
