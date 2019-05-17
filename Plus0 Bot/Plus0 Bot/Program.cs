using System;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Linq;
using NLog.Config;
using NLog.Targets;
using NLog;

namespace Plus0_Bot
{
    public static class Program
    {
        private static DiscordSocketClient client;
        private static CommandService commands;

        private static readonly Logger DiscordLogger = LogManager.GetLogger("Discord API - +0 Bot");

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

            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info
            });
            
            commands = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                LogLevel = LogSeverity.Debug
            });

            client.MessageReceived += Client_MessageReceived;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(),null);

            client.Ready += Client_Ready;
            client.Log += Client_Log;
            
            await client.LoginAsync(TokenType.Bot, Globals.BotSettings.BotToken);
            await client.StartAsync();
            await Task.Delay(-1);
        }

        private static Task Client_Log(LogMessage Message)
        {
            Console.WriteLine(Message);

            LogLevel logLevel;

            switch (Message.Severity)
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

            DiscordLogger.Log(logLevel, Message.ToString(prependTimestamp: false));

            return Task.CompletedTask;
        }

        private static async Task Client_Ready()
        {
        }

        private static async Task Client_MessageReceived(SocketMessage MessageParam)
        {
            SocketUserMessage message = MessageParam as SocketUserMessage;
            SocketCommandContext context = new SocketCommandContext(client, message);

            if (context.Message == null || context.Message.Content == "" || context.User.IsBot)
                return;

            int argPos = 0;
            if (!(message.HasStringPrefix("%", ref argPos)) || (message.HasMentionPrefix(client.CurrentUser, ref argPos)))
                return;

            IResult result = await commands.ExecuteAsync(context, argPos,null);

            if (!result.IsSuccess)
                Console.WriteLine($"[{DateTime.Now} at Commands] Something went wrong with executing a command. Text: {context.Message.Content} | Error: {result.ErrorReason}");
        }
    }
}
