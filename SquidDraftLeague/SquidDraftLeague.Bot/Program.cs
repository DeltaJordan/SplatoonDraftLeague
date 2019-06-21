using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Commands.Limitations;

namespace SquidDraftLeague.Bot
{
    public static class Program
    {
        public static DiscordSocketClient Client;
        private static CommandService commands;
        private static IServiceProvider services;

        private static readonly Logger ClassLogger = LogManager.GetCurrentClassLogger();
        private static readonly Logger DiscordLogger = LogManager.GetLogger("Discord API");

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

            LogManager.Configuration = config;

            string settingsLocation = Path.Combine(Globals.AppPath, "Data", "settings.json");
            string jsonFile = File.ReadAllText(settingsLocation);

            // Load the settings from file, then store it in the globals
            Globals.BotSettings = JsonConvert.DeserializeObject<Settings>(jsonFile);

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

            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

            Client.Ready += Client_Ready;
            Client.Log += Client_Log;
            
            await Client.LoginAsync(TokenType.Bot, Globals.BotSettings.BotToken);
            await Client.StartAsync();
            await Task.Delay(-1);
        }

        private static async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> messageCacheable, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                if (channel.Id == 588806681303973931)
                {
                    IUserMessage newUserMessage = (IUserMessage) await channel.GetMessageAsync(messageCacheable.Id);

                    if (!File.Exists(Path.Combine(Globals.AppPath, "Registrations", $"{newUserMessage.Id}")))
                    {
                        return;
                    }

                    if (newUserMessage.Content == "Approved.")
                    {
                        KeyValuePair<IEmote, ReactionMetadata> selectedNumberEmote = newUserMessage.Reactions
                            .Where(e => e.Key.Name != "\u274E" && e.Key.Name != "\u2705")
                            .OrderByDescending(e => e.Value.ReactionCount)
                            .FirstOrDefault();

                        if (selectedNumberEmote.Value.ReactionCount > 1)
                        {
                            string[] allRegLines = await File.ReadAllLinesAsync(Path.Combine(Globals.AppPath, "Registrations",
                                $"{newUserMessage.Id}"));

                            switch (selectedNumberEmote.Key.Name)
                            {
                                case "\u0031\u20E3":
                                    await AirTableClient.RegisterPlayer(
                                        Client.GetUser(Convert.ToUInt64(allRegLines[0])), 2200, allRegLines[1]);
                                    break;
                                case "\u0032\u20E3":
                                    await AirTableClient.RegisterPlayer(
                                        Client.GetUser(Convert.ToUInt64(allRegLines[0])), 2000, allRegLines[1]);
                                    break;
                                case "\u0033\u20E3":
                                    await AirTableClient.RegisterPlayer(
                                        Client.GetUser(Convert.ToUInt64(allRegLines[0])), 1800, allRegLines[1]);
                                    break;
                                case "\u0034\u20E3":
                                    await AirTableClient.RegisterPlayer(
                                        Client.GetUser(Convert.ToUInt64(allRegLines[0])), 1600, allRegLines[1]);
                                    break;
                            }

                            File.Delete(Path.Combine(Globals.AppPath, "Registrations", $"{newUserMessage.Id}"));
                        }
                    }

                    else if (newUserMessage.Reactions.FirstOrDefault(e => e.Key.Name == "\u2705").Value.ReactionCount > 1)
                    {
                        await newUserMessage.ModifyAsync(e => e.Content = "Approved.");
                    }
                    else if (newUserMessage.Reactions.FirstOrDefault(e => e.Key.Name == "\u274E").Value.ReactionCount > 1)
                    {
                        await newUserMessage.ModifyAsync(e => e.Content = "Denied.");
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

            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            {
                ClassLogger.Warn($"Something went wrong with executing a command. Text: {context.Message.Content} | Error: {result.ErrorReason}");
            }
        }
    }
}
