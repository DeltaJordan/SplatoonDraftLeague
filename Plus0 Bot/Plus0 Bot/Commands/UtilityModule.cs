using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MarkovSharp.TokenisationStrategies;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Plus0_Bot.Commands.Preconditions;

namespace Plus0_Bot.Commands
{
    public class UtilityModule : ModuleBase<SocketCommandContext>
    {
        [Command("sendhelp")]
        public async Task SendHelp()
        {
            try
            {
                string[] lines = File.ReadAllLines(Path.Combine(Globals.AppPath, "Data", "help.txt"));

                StringMarkov model = new StringMarkov(1);

                model.Learn(lines);

                await this.ReplyAsync(model.Walk().First());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("ping"), 
         Summary("Measures latency, probably inaccurate and is mainly to check the bot's status")]
        public async Task Ping()
        {
            await this.ReplyAsync($"Pong! ({(DateTimeOffset.Now - this.Context.Message.Timestamp).Milliseconds}ms)");
        }

        [Command("id"),
         Summary("Gets your discord id.")]
        public async Task Id()
        {
            await this.ReplyAsync($"Your Discord ID is {this.Context.User.Id}.");
        }

        [Command("suadd"),
         Summary("Adds a user to the superusers group."),
         RequireRole(null)]
        public async Task SuAdd(IUser user)
        {
            if (Globals.SuperUsers.Contains(user.Id))
            {
                await this.ReplyAsync("This user is already a su!");
                return;
            }

            File.WriteAllText(Path.Combine(Globals.AppPath, "Data", "superusers.json"), 
                JsonConvert.SerializeObject(Globals.SuperUsers.Append(user.Id).ToList(), Formatting.Indented));

            await this.ReplyAsync($"Added {user.Mention} to the superuser group.");
        }

        [Command("surm"),
         Summary("Removes a user from the superusers group."),
         RequireRole(null)]
        public async Task SuRm(IUser user)
        {
            File.WriteAllText(Path.Combine(Globals.AppPath, "Data", "superusers.json"), 
                JsonConvert.SerializeObject(Globals.SuperUsers.Select(e => e != user.Id).ToList(), Formatting.Indented));

            await this.ReplyAsync($"Removed {user.Mention} from the superuser group.");
        }

        [Command("eval"),
        Summary("Warning! Dangerous command, do not use unless you know what you're doing."),
        RequireRole("Developer")]
        public async Task Eval([Remainder] string command)
        {
            command = string.Join("\n", command.Split('\n').Skip(1).Take(command.Split('\n').Skip(1).Count() - 1));

            Console.WriteLine(command);

            ScriptRunner<object> script;

            try
            {
                script = CSharpScript.Create(command, ScriptOptions.Default
                    .WithReferences(typeof(object).GetTypeInfo().Assembly, typeof(Enumerable).GetTypeInfo().Assembly,
                                    typeof(PropertyInfo).GetTypeInfo().Assembly, typeof(Decoder).GetTypeInfo().Assembly,
                                    typeof(Regex).GetTypeInfo().Assembly, typeof(Task).GetTypeInfo().Assembly, typeof(CommandContext).GetTypeInfo().Assembly,
                                    typeof(MessageActivity).GetTypeInfo().Assembly, typeof(Settings).GetTypeInfo().Assembly)
                    .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Reflection", "System.Text",
                                 "System.Text.RegularExpressions", "System.Threading.Tasks", "Discord.Commands", "Discord", "Plus0_Bot",
                                 "Plus0_Bot.Commands", "Plus0_Bot.Commands.Preconditions"), typeof(GlobalEvalContext))
                    .CreateDelegate();
            }
            catch (Exception e)
            {
                EmbedBuilder errorBuilder = new EmbedBuilder();
                errorBuilder.WithTitle("Exception occurred.");
                errorBuilder.AddField("Input", $"```cs\n{command}\n```");
                errorBuilder.AddField("Output", $"```\n[Exception ({(e.InnerException ?? e).GetType().Name})] {e.InnerException?.Message ?? e.Message}\n```");
                errorBuilder.WithColor(Color.Red);

                await this.Context.Channel.SendMessageAsync(null, false, errorBuilder.Build());

                return;
            }

            object result;

            try
            {
                result = await script(new GlobalEvalContext
                {
                    Ctx = this.Context
                });
            }
            catch (Exception e)
            {
                EmbedBuilder errorBuilder = new EmbedBuilder();
                errorBuilder.WithTitle("Exception occurred.");
                errorBuilder.AddField("Input", $"```cs\n{command}\n```");
                errorBuilder.AddField("Output", $"```\n[Exception ({(e.InnerException ?? e).GetType().Name})] {e.InnerException?.Message ?? e.Message}\n```");
                errorBuilder.WithColor(Color.Red);

                await this.Context.Channel.SendMessageAsync(null, false, errorBuilder.Build());

                return;
            }

            EmbedBuilder builder = new EmbedBuilder();
            builder.AddField("Input", $"```cs\n{command}\n```");
            builder.AddField("Output", $"```\n{result}\n```");
            builder.WithColor(Color.Green);

            await this.Context.Channel.SendMessageAsync(null, false, builder.Build());
        }
    }

    public class GlobalEvalContext
    {
        public SocketCommandContext Ctx { get; set; }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once UnusedMember.Global
        public SocketCommandContext ctx => this.Ctx;
    }
}
