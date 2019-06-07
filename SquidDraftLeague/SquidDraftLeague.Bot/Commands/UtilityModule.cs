using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
using NLog;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Commands.Preconditions;
using SquidDraftLeague.Bot.Queuing.Data;
using Image = SixLabors.ImageSharp.Image;
using Path = System.IO.Path;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Misc.")]
    public class UtilityModule : ModuleBase<SocketCommandContext>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

        [Command("profile")]
        public async Task Profile([Remainder] IUser user = null)
        {
            try
            {
                if (user == null)
                {
                    user = this.Context.User;
                }

                SdlPlayer player = await AirTableClient.RetrieveSdlPlayer((IGuildUser) user);

                Font powerFont = Globals.ProfileFontFamily.CreateFont(400);
                Font nameFont = Globals.ProfileFontFamily.CreateFont(220);
                Font winRateFont = Globals.ProfileFontFamily.CreateFont(350);

                WebClient webClient = new WebClient();
                byte[] avatarBytes = await webClient.DownloadDataTaskAsync(user.GetAvatarUrl());

                using (Image<Rgba32> image = Image.Load(Path.Combine(Globals.AppPath, "Data", "profile-template.png")))
                using (Image<Rgba32> rankImage = Image.Load(Path.Combine(Globals.AppPath, "Data", "XSABC.png")))
                using (Image<Rgba32> avatarImage = Image.Load(avatarBytes))
                using (MemoryStream ms = new MemoryStream())
                {
                    string name = player.AirtableName;
                    string powerLevel = Math.Round(player.PowerLevel, 1).ToString(CultureInfo.InvariantCulture);

                    SizeF nameTextSize = TextMeasurer.Measure(name, new RendererOptions(nameFont));

                    if (nameTextSize.Width > 2365)
                    {
                        float nameScalingFactor = 2365 / nameTextSize.Width;
                        nameFont = Globals.ProfileFontFamily.CreateFont(nameFont.Size * nameScalingFactor);

                        nameTextSize = TextMeasurer.Measure(name, new RendererOptions(nameFont));
                    }

                    // Note that the Y values for the fonts are offset by 20. Might need to look into that to see why.
                    float nameTextX = 1150 + (1232 - nameTextSize.Width / 2);
                    float nameTextY = 120 + 20 + (162 - nameTextSize.Height / 2);

                    IPathCollection nameTextGlyphs = TextBuilder.GenerateGlyphs(name,
                        new PointF(nameTextX, nameTextY), new RendererOptions(nameFont));

                    SizeF powerTextSize = TextMeasurer.Measure(powerLevel,
                        new RendererOptions(powerFont));

                    float powerScalingFactor = (687 - 100) / powerTextSize.Height;
                    powerFont = Globals.ProfileFontFamily.CreateFont(powerFont.Size * powerScalingFactor);
                    powerTextSize = TextMeasurer.Measure(powerLevel,
                        new RendererOptions(powerFont));

                    float powerTextX = 1150 + (1232 - powerTextSize.Width / 2);
                    float powerTextY = 450 + 20 + (342 - powerTextSize.Height / 2);

                    IPathCollection powerTextGlyphs = TextBuilder.GenerateGlyphs(powerLevel,
                        new PointF(powerTextX, powerTextY), new RendererOptions(powerFont));

                    SizeF switchCodeSize = TextMeasurer.Measure(player.SwitchFriendCode, new RendererOptions(nameFont));

                    float switchCodeX = (float) image.Width / 2 - switchCodeSize.Width / 2;
                    float switchCodeY = 2685 + 20 + (190 - switchCodeSize.Height / 2);

                    IPathCollection switchCodeGlyphs = TextBuilder.GenerateGlyphs(player.SwitchFriendCode,
                        new PointF(switchCodeX, switchCodeY), new RendererOptions(nameFont));

                    IPathCollection splatZonesGlyphs = TextBuilder.GenerateGlyphs(
                        $"{player.WinRates[GameMode.SplatZones]:P0}", new PointF(1345, 1830),
                        new RendererOptions(winRateFont));

                    IPathCollection rainmakerGlyphs = TextBuilder.GenerateGlyphs(
                        $"{player.WinRates[GameMode.SplatZones]:P0}", new PointF(1345, 2340),
                        new RendererOptions(winRateFont));

                    IPathCollection towerControlGlyphs = TextBuilder.GenerateGlyphs(
                        $"{player.WinRates[GameMode.SplatZones]:P0}", new PointF(3455, 2340),
                        new RendererOptions(winRateFont));

                    IPathCollection clamBlitzGlyphs = TextBuilder.GenerateGlyphs(
                        $"{player.WinRates[GameMode.SplatZones]:P0}", new PointF(3455, 1830),
                        new RendererOptions(winRateFont));

                    TextGraphicsOptions textGraphicsOptions = new TextGraphicsOptions(true);

                    int rankCropY = 0;

                    if (player.PowerLevel < 1200)
                    {
                        rankImage.Mutate(e => e
                            .Fill(Rgba32.Black));
                    }
                    else if (player.PowerLevel > 2200)
                    {
                        rankCropY = 0;
                    }
                    else
                    {
                        rankCropY = rankImage.Height - ((int) player.PowerLevel - 1000) / 200 * 500;
                    }

                    rankImage.Mutate(e => e
                        .Crop(new Rectangle(0, rankCropY, 500, 500))
                        .Resize(new Size(868, 868)));

                    avatarImage.Mutate(e => e
                        .Resize(new Size(856, 856)));

                    image.Mutate(e => e
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.White, powerTextGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, nameTextGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, switchCodeGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, splatZonesGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, rainmakerGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, towerControlGlyphs)
                        .Fill((GraphicsOptions) textGraphicsOptions, Rgba32.Black, clamBlitzGlyphs)
                        .DrawImage(avatarImage, new Point(128, 125), 1)
                        .DrawImage(rankImage, new Point(3763, 116), 1));

                    image.SaveAsPng(ms);

                    using (MemoryStream memory = new MemoryStream(ms.GetBuffer()))
                    {
                        await this.Context.Channel.SendFileAsync(memory, "profile.png");
                    }
                }

                Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }

        [Command("stage"),
         Summary("Gets a random stage and mode.")]
        public async Task Stage()
        {
            try
            {
                Stage[] stages = await AirTableClient.GetMapList();

                Stage selectedStage = stages[Globals.Random.Next(0, stages.Length - 1)];

                await this.ReplyAsync(embed: selectedStage.GetEmbedBuilder().Build());
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }

        [Command("ping"), 
         Summary("Measures latency, probably inaccurate and is mainly to check the bot's status")]
        public async Task Ping()
        {
            IUserMessage message = await this.ReplyAsync("Ping?");
            await message.ModifyAsync(e =>
                e.Content =
                    $"Pong! ({(message.CreatedAt - this.Context.Message.CreatedAt).Milliseconds}ms)");
        }

        [Command("id"),
         Summary("Gets your discord id.")]
        public async Task Id()
        {
            await this.ReplyAsync($"Your Discord ID is `{this.Context.User.Id}`.");
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
        Summary("Warning! Dangerous command, do not use unless you know what you're doing.")]
        public async Task Eval([Remainder] string command)
        {
            if (this.Context.User.Id != 228019100008316948)
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
                                    typeof(MessageActivity).GetTypeInfo().Assembly, typeof(Settings).GetTypeInfo().Assembly)
                    .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Reflection", "System.Text",
                                 "System.Text.RegularExpressions", "System.Threading.Tasks", "Discord.Commands", "Discord", "SquidDraftLeague.Bot",
                                 "SquidDraftLeague.Bot.Commands", "SquidDraftLeague.Bot.Commands.Preconditions"), typeof(GlobalEvalContext))
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
