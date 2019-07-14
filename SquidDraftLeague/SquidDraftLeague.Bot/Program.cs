using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using NLog.Conditions;
using NLog.Config;
using NLog.Targets;
using SquidDraftLeague.AirTable;
using SquidDraftLeague.Bot.Commands;
using SquidDraftLeague.Bot.Commands.Limitations;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot
{
    public static class Program
    {
        public static DiscordSocketClient Client;
        private static CommandService commands;
        private static IServiceProvider services;

        private static readonly Logger ClassLogger = LogManager.GetCurrentClassLogger();
        private static readonly Logger DiscordLogger = LogManager.GetLogger("Discord API");

        private static Timer happyNotificationTimer;
        private static Timer halfNotificationTimer;

        /// <summary>
        /// Main async method for the bot.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            // Make sure Log folder exists
            Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Logs"));

            // Checks for existing latest log
            if (File.Exists(Path.Combine(Globals.AppPath, "Logs", "latest.log")))
            {
                // This is no longer the latest log; move to backlogs
                string oldLogFileName = File.ReadAllLines(Path.Combine(Globals.AppPath, "Logs", "latest.log"))[0];
                File.Move(Path.Combine(Globals.AppPath, "Logs", "latest.log"), Path.Combine(Globals.AppPath, "Logs", oldLogFileName));
            }

            // Builds a file name to prepare for future backlogging
            string logFileName = $"{DateTime.Now:dd-MM-yy}-1.log";

            // Loops until the log file doesn't exist
            int index = 2;
            while (File.Exists(Path.Combine(Globals.AppPath, "Logs", logFileName)))
            {
                logFileName = $"{DateTime.Now:dd-MM-yy}-{index}.log";
                index++;
            }

            // Logs the future backlog file name
            File.WriteAllText(Path.Combine(Globals.AppPath, "Logs", "latest.log"), $"{logFileName}\n");

            // Set up logging through NLog
            LoggingConfiguration config = new LoggingConfiguration();

            FileTarget logfile = new FileTarget("logfile")
            {
                FileName = Path.Combine(Globals.AppPath, "Logs", "latest.log"),
                Layout = "[${time}] [${level:uppercase=true}] [${logger}] ${message}"
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);


            ColoredConsoleTarget coloredConsoleTarget = new ColoredConsoleTarget
            {
                UseDefaultRowHighlightingRules = true
            };
            config.AddRule(LogLevel.Info, LogLevel.Fatal, coloredConsoleTarget);
            LogManager.Configuration = config;

            string settingsLocation = Path.Combine(Globals.AppPath, "Data", "settings.json");
            string jsonFile = File.ReadAllText(settingsLocation);

            // Load the settings from file, then store it in the globals
            Globals.BotSettings = JsonConvert.DeserializeObject<Settings.Settings>(jsonFile);

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });
            
            commands = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                SeparatorChar = ' ',
                DefaultRunMode = RunMode.Async,
                LogLevel = LogSeverity.Debug
            });

            services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton<InteractiveService>()
                .BuildServiceProvider();

            Client.MessageReceived += Client_MessageReceived;
            Client.ReactionAdded += Client_ReactionAdded;
            Client.UserLeft += Client_UserLeft;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

            Client.Ready += Client_Ready;
            Client.Log += Client_Log;

            await Client.LoginAsync(TokenType.Bot, Globals.BotSettings.BotToken);
            await Client.StartAsync();

            TimeSpan dayLength = new TimeSpan(24, 0, 0);
            TimeSpan nowTimeSpan = DateTime.UtcNow.TimeOfDay;
            TimeSpan happyActivationTime = TimeSpan.Parse("20:00");
            TimeSpan happyActivationInterval = dayLength - nowTimeSpan + happyActivationTime;
            happyActivationInterval = happyActivationInterval.TotalHours > 24
                ? happyActivationInterval - new TimeSpan(24, 0, 0)
                : happyActivationInterval;

            happyNotificationTimer = new Timer
            {
                Interval = happyActivationInterval.TotalMilliseconds
            };

            happyNotificationTimer.Elapsed += HappyNotificationTimer_Elapsed;
            happyNotificationTimer.Start();

            TimeSpan halfActivationTime = TimeSpan.Parse("1:00");
            TimeSpan halfActivationInterval = dayLength - nowTimeSpan + halfActivationTime;
            halfActivationInterval = halfActivationInterval.TotalHours > 24
                ? halfActivationInterval - new TimeSpan(24, 0, 0)
                : halfActivationInterval;

            halfNotificationTimer = new Timer
            {
                Interval = halfActivationInterval.TotalMilliseconds
            };

            halfNotificationTimer.Elapsed += HalfNotificationTimer_Elapsed;
            halfNotificationTimer.Start();

            await Task.Delay(-1);
        }

        private static async void HalfNotificationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await Client.GetGuild(570743985530863649).GetTextChannel(572536965833162753).SendMessageAsync(
                "<@&592448366831730708> Half points hour has begun! " +
                "Any sets started in the next hour will lose half points when losing. " +
                "Points won are not affected.");

            halfNotificationTimer.Interval = new TimeSpan(24, 0, 0).TotalMilliseconds;
            halfNotificationTimer.Stop();
            halfNotificationTimer.Start();
        }

        private static async void HappyNotificationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await Client.GetGuild(570743985530863649).GetTextChannel(572536965833162753).SendMessageAsync(
                "<@&592448366831730708> Double points hour has begun! " +
                "Any sets started in the next hour will gain double points when winning. " +
                "Points lost are not affected.");

            happyNotificationTimer.Interval = new TimeSpan(24, 0, 0).TotalMilliseconds;
            happyNotificationTimer.Stop();
            happyNotificationTimer.Start();
        }

        private static async Task Client_UserLeft(SocketGuildUser arg)
        {
            if (arg.Guild.Id == 570743985530863649)
            {
                await arg.Guild.GetTextChannel(579790669007290370)
                    .SendMessageAsync($"{arg.Username}#{arg.DiscriminatorValue} ({arg.Mention}) has left the server.");

                if (Matchmaker.Lobbies.Any(e => e.Players.Any(f => f.DiscordId == arg.Id)))
                {
                    Lobby lobby = Matchmaker.Lobbies.First(e => e.Players.Any(f => f.DiscordId == arg.Id));
                    lobby.RemovePlayer(lobby.Players.First(e => e.DiscordId == arg.Id));

                    await arg.Guild.GetTextChannel(572536965833162753).SendMessageAsync(
                        $"{arg.Username} has left the server. They have been forcefully removed from lobby #{lobby.LobbyNumber}.");
                }
                else if (Matchmaker.Sets.Any(e => e.AllPlayers.Any(f => f.DiscordId == arg.Id)))
                {
                    Set set = Matchmaker.Sets.First(e => e.AllPlayers.Any(f => f.DiscordId == arg.Id));

                    await CommandHelper.ChannelFromSet(set.SetNumber).SendMessageAsync($"<@&572539082039885839> {arg.Username} left the server. Force closing the set.");

                    if (set.AlphaTeam.Players.Any(e => e.DiscordId == arg.Id))
                    {
                        set.AlphaTeam.RemovePlayer(set.AllPlayers.First(e => e.DiscordId == arg.Id));
                    }
                    else
                    {
                        set.BravoTeam.RemovePlayer(set.AllPlayers.First(e => e.DiscordId == arg.Id));
                    }

                    List<SocketRole> roleRemovalList = CommandHelper.DraftRoleIds.Select(e => arg.Guild.GetRole(e)).ToList();

                    foreach (SdlPlayer sdlPlayer in set.AllPlayers)
                    {
                        await arg.Guild.GetUser(sdlPlayer.DiscordId).RemoveRolesAsync(roleRemovalList);
                    }

                    set.Close();
                }
            }
        }

        private static async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> messageCacheable, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                if (channel.Id == 595219144488648704)
                {
                    IUserMessage newUserMessage = (IUserMessage) await channel.GetMessageAsync(messageCacheable.Id);
                    ITextChannel registeredChannel = (ITextChannel) Client.GetChannel(588806681303973931);

                    if (!File.Exists(Path.Combine(Globals.AppPath, "Registrations", $"{newUserMessage.Id}")))
                    {
                        return;
                    }

                    if (newUserMessage.Content == "Approved.")
                    {
                        (IEmote emote, ReactionMetadata reactionMetadata) = newUserMessage.Reactions
                            .Where(e => e.Key.Name != "\u274E" && e.Key.Name != "\u2705")
                            .OrderByDescending(e => e.Value.ReactionCount)
                            .FirstOrDefault();

                        if (reactionMetadata.ReactionCount > 1)
                        {
                            string[] allRegLines = await File.ReadAllLinesAsync(Path.Combine(Globals.AppPath, "Registrations",
                                $"{newUserMessage.Id}"));

                            int classNum = 0;

                            ulong userId = Convert.ToUInt64(allRegLines[0]);

                            switch (emote.Name)
                            {
                                case "\u0031\u20E3":
                                    await AirTableClient.RegisterPlayer(userId, 2200, allRegLines[1]);
                                    classNum = 1;
                                    break;
                                case "\u0032\u20E3":
                                    await AirTableClient.RegisterPlayer(userId, 2000, allRegLines[1]);
                                    classNum = 2;
                                    break;
                                case "\u0033\u20E3":
                                    await AirTableClient.RegisterPlayer(userId, 1800, allRegLines[1]);
                                    classNum = 3;
                                    break;
                                case "\u0034\u20E3":
                                    await AirTableClient.RegisterPlayer(userId, 1700, allRegLines[1]);
                                    classNum = 4;
                                    break;
                            }

                            SocketGuild guild = Client.GetGuild(570743985530863649);
                            SocketGuildUser registeredUser = guild.GetUser(userId);
                            IDMChannel dmChannel = await registeredUser.GetOrCreateDMChannelAsync();
                            await dmChannel.SendMessageAsync($"You have been approved! You have been placed in class {classNum}. " +
                                                             $"To jump into a set, head into #draft and use %join.");

                            await registeredUser.AddRoleAsync(guild.GetRole(572537013949956105));
                            await registeredUser.AddRoleAsync(guild.GetRole(592448366831730708));

                            File.Delete(Path.Combine(Globals.AppPath, "Registrations", $"{newUserMessage.Id}"));

                            IEmbed registrationEmbed = newUserMessage.Embeds.First();

                            EmbedBuilder builder = new EmbedBuilder
                            {
                                Description =
                                    $"**User {registeredUser.Mention} ({registeredUser.Username}#{registeredUser.DiscriminatorValue}) has been approved!**"
                            };

                            builder.AddField(e =>
                            {
                                e.Name = "Class";
                                e.Value = $"{classNum}";
                                e.IsInline = false;
                            });

                            builder.WithFields(registrationEmbed.Fields.Select(e =>
                            {
                                EmbedFieldBuilder builderSelect = new EmbedFieldBuilder
                                {
                                    Name = e.Name,
                                    Value = e.Value,
                                    IsInline = e.Inline
                                };

                                return builderSelect;
                            }));

                            if (registrationEmbed.Image.HasValue)
                            {
                                builder.ImageUrl = registrationEmbed.Image.Value.Url;
                            }

                            await registeredChannel.SendMessageAsync(embed: builder.Build());

                            await newUserMessage.DeleteAsync();
                        }
                    }

                    else if (newUserMessage.Reactions.FirstOrDefault(e => e.Key.Name == "\u2705").Value.ReactionCount > 1)
                    {
                        await newUserMessage.ModifyAsync(e => e.Content = "Approved.");

                    }
                    else if (newUserMessage.Reactions.FirstOrDefault(e => e.Key.Name == "\u274E").Value.ReactionCount > 1)
                    {
                        string[] allRegLines = await File.ReadAllLinesAsync(Path.Combine(Globals.AppPath, "Registrations",
                            $"{newUserMessage.Id}"));

                        ulong userId = Convert.ToUInt64(allRegLines[0]);

                        SocketGuild guild = Client.GetGuild(570743985530863649);
                        SocketGuildUser registeredUser = guild.GetUser(userId);

                        IEmbed registrationEmbed = newUserMessage.Embeds.First();

                        EmbedBuilder builder = new EmbedBuilder
                        {
                            Description =
                                $"**User {registeredUser.Mention} ({registeredUser.Username}#{registeredUser.DiscriminatorValue}) has been denied.**"
                        };

                        builder.WithFields(registrationEmbed.Fields.Select(e =>
                        {
                            EmbedFieldBuilder builderSelect = new EmbedFieldBuilder
                            {
                                Name = e.Name,
                                Value = e.Value,
                                IsInline = e.Inline
                            };

                            return builderSelect;
                        }));

                        if (registrationEmbed.Image.HasValue)
                        {
                            builder.ImageUrl = registrationEmbed.Image.Value.Url;
                        }

                        await registeredChannel.SendMessageAsync(embed: builder.Build());

                        await newUserMessage.DeleteAsync();
                        File.Delete(Path.Combine(Globals.AppPath, "Registrations", $"{newUserMessage.Id}"));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static Task Client_Log(LogMessage message)
        {
            LogLevel logLevel;

            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    logLevel = LogLevel.Fatal;
                    break;
                case LogSeverity.Error:
                    logLevel = LogLevel.Error;
                    break;
                case LogSeverity.Warning:
                    logLevel = LogLevel.Warn;
                    break;
                case LogSeverity.Info:
                    logLevel = LogLevel.Info;
                    break;
                case LogSeverity.Verbose:
                    logLevel = LogLevel.Trace;
                    break;
                case LogSeverity.Debug:
                    logLevel = LogLevel.Debug;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            if (logLevel >= LogLevel.Info)
                Console.WriteLine(message);

            DiscordLogger.Log(logLevel, message.ToString(prependTimestamp: false));

            return Task.CompletedTask;
        }

        private static async Task Client_Ready()
        {
        }

        private static async Task Client_MessageReceived(SocketMessage messageParam)
        {
            SocketUserMessage message = messageParam as SocketUserMessage;
            SocketCommandContext context = new SocketCommandContext(Client, message);

            if (context.Message == null || context.Message.Content == "" || context.User.IsBot)
                return;

            int argPos = 0;
#if DEBUG_PREFIX
            string prefix = Globals.BotSettings.Prefix + Globals.BotSettings.Prefix;
#else
            string prefix = Globals.BotSettings.Prefix;
#endif

            if (!message.HasStringPrefix(prefix, ref argPos) || message.HasMentionPrefix(Client.CurrentUser, ref argPos))
                return;

            string limitDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Limiters")).FullName;

            if (context.Channel is IGuildChannel)
            {
                foreach (CommandMatch command in commands.Search(context, argPos).Commands)
                {
                    if (File.Exists(Path.Combine(limitDirectory, "all")))
                    {
                        CommandLimiter commandLimiter =
                            JsonConvert.DeserializeObject<CommandLimiter>(
                                await File.ReadAllTextAsync(Path.Combine(limitDirectory, "all")),
                                new JsonSerializerSettings
                                {
                                    TypeNameHandling = TypeNameHandling.Auto
                                });

                        if (!await commandLimiter.CheckAllLimitationsAsync(context))
                        {
                            return;
                        }
                    }

                    if (File.Exists(Path.Combine(limitDirectory, $"{command.Command.Name}")))
                    {
                        CommandLimiter commandLimiter =
                            JsonConvert.DeserializeObject<CommandLimiter>(
                                await File.ReadAllTextAsync(Path.Combine(limitDirectory, $"{command.Command.Name}")),
                                new JsonSerializerSettings
                                {
                                    TypeNameHandling = TypeNameHandling.Auto
                                });

                        if (!await commandLimiter.CheckAllLimitationsAsync(context))
                        {
                            return;
                        }
                    }

                    if (File.Exists(Path.Combine(limitDirectory, $"{command.Command.Module.Name}")))
                    {
                        CommandLimiter commandLimiter =
                            JsonConvert.DeserializeObject<CommandLimiter>(
                                await File.ReadAllTextAsync(Path.Combine(limitDirectory,
                                    $"{command.Command.Module.Name}")),
                                new JsonSerializerSettings
                                {
                                    TypeNameHandling = TypeNameHandling.Auto
                                });

                        if (!await commandLimiter.CheckAllLimitationsAsync(context))
                        {
                            return;
                        }
                    }
                }
            }

            IResult result = await commands.ExecuteAsync(context, argPos, services);

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.UnknownCommand:
                        ClassLogger.Warn($"Unable to find command that matches \"{context.Message.Content}\".");
                        break;
                    case CommandError.ParseFailed:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case CommandError.BadArgCount:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case CommandError.ObjectNotFound:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case CommandError.MultipleMatches:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case CommandError.UnmetPrecondition:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case CommandError.Exception:
                        if (result is ExecuteResult executeResult)
                        {
                            ClassLogger.Error(executeResult.Exception);
                        }
                        break;
                    case CommandError.Unsuccessful:
                        ClassLogger.Warn($"{result.ErrorReason} | Message: {context.Message.Content}");
                        break;
                    case null:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            /*else if (result.Error == CommandError.UnknownCommand)
            {
                string moduleFolder = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Modules")).FullName;

                string moduleFile = Path.Combine(moduleFolder, $"{message.Content.Substring(argPos).Split(' ')[0]}");


                if (File.Exists(moduleFile))
                {

                }
            }*/
        }
    }
}
