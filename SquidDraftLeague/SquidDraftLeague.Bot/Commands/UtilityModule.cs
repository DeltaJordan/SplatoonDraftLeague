using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MarkovSharp.TokenisationStrategies;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using NLog;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Map;
using SquidDraftLeague.Draft.Matchmaking;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.Settings;
using Image = SixLabors.ImageSharp.Image;
using Path = System.IO.Path;

namespace SquidDraftLeague.Bot.Commands
{
    public class UtilityModule
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Command("notif")]
        public async Task Notif(CommandContext ctx)
        {
            if (!(ctx.User is DiscordMember user))
                return;

            if (user.Roles.All(e => e.Name != "Player"))
                return;

            SdlPlayer player = await MySqlClient.RetrieveSdlPlayer(user.Id);

            DiscordRole classOneRole = ctx.Guild.GetRole(600770643075661824);
            DiscordRole classTwoRole = ctx.Guild.GetRole(600770814521901076);
            DiscordRole classThreeRole = ctx.Guild.GetRole(600770862307606542);
            DiscordRole classFourRole = ctx.Guild.GetRole(600770905282576406);

            DiscordRole selectedRole = null;

            switch (Matchmaker.GetClass(player.PowerLevel))
            {
                case SdlClass.Zero:
                    break;
                case SdlClass.One:
                    selectedRole = classOneRole;
                    break;
                case SdlClass.Two:
                    selectedRole = classTwoRole;
                    break;
                case SdlClass.Three:
                    selectedRole = classThreeRole;
                    break;
                case SdlClass.Four:
                    selectedRole = classFourRole;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            string optOutDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Opt Out")).FullName;

            if (user.Roles.Any(e => e.Id == selectedRole?.Id))
            {
                await File.WriteAllTextAsync(Path.Combine(optOutDirectory, $"{ctx.User.Id}.dat"), "bruh");

                await user.RevokeRoleAsync(selectedRole);
                await ctx.RespondAsync("Disabled lobby notifications.");
            }
            else
            {
                if (File.Exists(Path.Combine(optOutDirectory, $"{ctx.User.Id}.dat")))
                    File.Delete(Path.Combine(optOutDirectory, $"{ctx.User.Id}.dat"));

                await user.RevokeRoleAsync(selectedRole);
                await ctx.RespondAsync("Enabled lobby notifications.");
            }
        }

        [Command("distributeRoles")]
        public async Task DistributeRoles(CommandContext ctx)
        {
            DiscordRole classOneRole = ctx.Guild.GetRole(600770643075661824);
            DiscordRole classTwoRole = ctx.Guild.GetRole(600770814521901076);
            DiscordRole classThreeRole = ctx.Guild.GetRole(600770862307606542);
            DiscordRole classFourRole = ctx.Guild.GetRole(600770905282576406);

            foreach (SdlPlayer sdlPlayer in await MySqlClient.RetrieveAllSdlPlayers())
            {
                try
                {
                    DiscordMember sdlGuildUser = await ctx.Guild.GetMemberAsync(sdlPlayer.DiscordId);

                    switch (Matchmaker.GetClass(sdlPlayer.PowerLevel))
                    {
                        case SdlClass.Zero:
                            break;
                        case SdlClass.One:
                            if (sdlGuildUser.Roles.All(e => e.Id != classOneRole.Id))
                            {
                                await sdlGuildUser.GrantRoleAsync(classOneRole);
                            }
                            break;
                        case SdlClass.Two:
                            if (sdlGuildUser.Roles.All(e => e.Id != classTwoRole.Id))
                            {
                                await sdlGuildUser.GrantRoleAsync(classTwoRole);
                            }
                            break;
                        case SdlClass.Three:
                            if (sdlGuildUser.Roles.All(e => e.Id != classThreeRole.Id))
                            {
                                await sdlGuildUser.GrantRoleAsync(classThreeRole);
                            }
                            break;
                        case SdlClass.Four:
                            if (sdlGuildUser.Roles.All(e => e.Id != classFourRole.Id))
                            {
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

            await ctx.RespondAsync("I'm Sam, the dancing Matzo man. Making Matzos fast I can. I'm Sam the dancing Matzo man.");
        }

        [Command("sendhelp")]
        public async Task SendHelp(CommandContext ctx)
        {
            try
            {
                string[] lines = File.ReadAllLines(Path.Combine(Globals.AppPath, "Data", "help.txt"));

                StringMarkov model = new StringMarkov(1);

                model.Learn(lines);

                await ctx.RespondAsync(model.Walk().First());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("stage"),
         Description("Gets a random stage and mode.")]
        public async Task Stage(CommandContext ctx)
        {
            try
            {
                Stage[] stages = await MySqlClient.GetMapList();

                Stage selectedStage = stages[Globals.Random.Next(0, stages.Length - 1)];

                await ctx.RespondAsync(embed: selectedStage.GetEmbedBuilder().Build());
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }

        [Command("ping"),
         Description("Measures latency, probably inaccurate and is mainly to check the bot's status")]
        public async Task Ping(CommandContext ctx)
        {
            DiscordMessage message = await ctx.RespondAsync("Ping?");
            await message.ModifyAsync($"Pong! API latency is {Program.Client.Ping}ms.");
        }

        [Command("id"),
         Description("Gets your discord id.")]
        public async Task Id(CommandContext ctx)
        {
            await ctx.RespondAsync($"Your Discord ID is `{ctx.User.Id}`.");
        }

        [Command("suadd"),
         Description("Adds a user to the superusers group."),
         RequireRole(null)]
        public async Task SuAdd(CommandContext ctx, DiscordMember user)
        {
            if (Globals.SuperUsers.Contains(user.Id))
            {
                await ctx.RespondAsync("This user is already a su!");
                return;
            }

            File.WriteAllText(Path.Combine(Globals.AppPath, "Data", "superusers.json"), 
                JsonConvert.SerializeObject(Globals.SuperUsers.Append(user.Id).ToList(), Formatting.Indented));

            await ctx.RespondAsync($"Added {user.Mention} to the superuser group.");
        }

        [Command("surm"),
         Description("Removes a user from the superusers group."),
         RequireRole(null)]
        public async Task SuRm(CommandContext ctx, DiscordMember user)
        {
            File.WriteAllText(Path.Combine(Globals.AppPath, "Data", "superusers.json"), 
                JsonConvert.SerializeObject(Globals.SuperUsers.Select(e => e != user.Id).ToList(), Formatting.Indented));

            await ctx.RespondAsync($"Removed {user.Mention} from the superuser group.");
        }

        [Command("addmod")]
        public async Task AddModule(CommandContext ctx, string name, [RemainingText] string command)
        {
            if (ctx.User.Id != 228019100008316948)
            {
                return;
            }
            
            command = string.Join("\n", command.Split('\n').Skip(1).Take(command.Split('\n').Skip(1).Count() - 1));

            string moduleFolder = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Modules")).FullName;

            await File.WriteAllTextAsync(Path.Combine(moduleFolder, $"{name}"), command);
        }

        [Command("eval"),
        Description("Warning! Dangerous command, do not use unless you know what you're doing.")]
        public async Task Eval(CommandContext ctx, [RemainingText] string command)
        {
            if (ctx.User.Id != 228019100008316948)
            {
                return;
            }

            command = string.Join("\n", command.Split('\n').Skip(1).Take(command.Split('\n').Skip(1).Count() - 1));

            ScriptRunner<object> script;

            try
            {
                script = CSharpScript.Create(command, ScriptOptions.Default
                    .WithReferences(typeof(object).GetTypeInfo().Assembly, typeof(Enumerable).GetTypeInfo().Assembly,
                                    typeof(PropertyInfo).GetTypeInfo().Assembly, typeof(Decoder).GetTypeInfo().Assembly,
                                    typeof(Regex).GetTypeInfo().Assembly, typeof(Task).GetTypeInfo().Assembly, typeof(CommandContext).GetTypeInfo().Assembly,
                                    typeof(DiscordMessage).GetTypeInfo().Assembly, typeof(Settings.Settings).GetTypeInfo().Assembly,
                                    typeof(Program).Assembly, typeof(MySqlClient).Assembly, typeof(Lobby).Assembly)
                    .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Reflection", "System.Text",
                                 "System.Text.RegularExpressions", "System.Threading.Tasks", "DSharpPlus.CommandsNext", "DSharpPlus", "SquidDraftLeague.Bot",
                                 "SquidDraftLeague.Bot.Commands", "SquidDraftLeague.Settings", "SquidDraftLeague.Draft", "SquidDraftLeague.MySQL",
                                 "SquidDraftLeague.Bot.Commands.Preconditions"), typeof(GlobalEvalContext))
                    .CreateDelegate();
            }
            catch (Exception e)
            {
                DiscordEmbedBuilder errorBuilder = new DiscordEmbedBuilder();
                errorBuilder.WithTitle("Exception occurred.");
                errorBuilder.AddField("Input", $"```cs\n{command}\n```");
                errorBuilder.AddField("Output", $"```\n[Exception ({(e.InnerException ?? e).GetType().Name})] {e.InnerException?.Message ?? e.Message}\n```");
                errorBuilder.WithColor(Color.Red);

                await ctx.Channel.SendMessageAsync(null, false, errorBuilder.Build());

                return;
            }

            object result;

            try
            {
                result = await script(new GlobalEvalContext
                {
                    Ctx = ctx
                });
            }
            catch (Exception e)
            {
                DiscordEmbedBuilder errorBuilder = new DiscordEmbedBuilder();
                errorBuilder.WithTitle("Exception occurred.");
                errorBuilder.AddField("Input", $"```cs\n{command}\n```");
                errorBuilder.AddField("Output", $"```\n[Exception ({(e.InnerException ?? e).GetType().Name})] {e.InnerException?.Message ?? e.Message}\n```");
                errorBuilder.WithColor(Color.Red);

                await ctx.Channel.SendMessageAsync(null, false, errorBuilder.Build());

                return;
            }

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            builder.AddField("Input", $"```cs\n{command}\n```");
            builder.AddField("Output", $"```\n{result}\n```");
            builder.WithColor(Color.Green);

            await ctx.Channel.SendMessageAsync(null, false, builder.Build());
        }
    }

    public class GlobalEvalContext
    {
        public CommandContext Ctx { get; set; }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once UnusedMember.Global
#pragma warning disable IDE1006 // Naming Styles
        public CommandContext ctx => this.Ctx;
#pragma warning restore IDE1006 // Naming Styles
    }
}
