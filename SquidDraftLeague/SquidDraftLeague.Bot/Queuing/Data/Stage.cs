using System;
using System.Net;
using Discord;
using HtmlAgilityPack;
using NLog;

namespace SquidDraftLeague.Bot.Queuing.Data
{
    public class Stage
    {
        public string MapName { get; set; }

        public string MapNameEscaped => this.MapName.Replace(" ", "_");

        public GameMode Mode { get; set; }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string GetMapImageLink()
        {
            try
            {
                WebClient client = new WebClient();
                HtmlDocument document = new HtmlDocument();
                document.Load(client.OpenRead($"https://splatoonwiki.org/wiki/File:S2_Stage_{this.MapNameEscaped}.png"));
                HtmlNode selectNode = document.DocumentNode.SelectSingleNode("//a[contains(@class, 'internal')]");
                string imageUrl = "https:" + selectNode.Attributes["href"].Value;
                return imageUrl;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Error occured for stage {this.MapName}");
                throw;
            }
        }

        public string GetModeName()
        {
            switch (this.Mode)
            {
                case GameMode.TowerControl:
                    return "Tower Control";
                case GameMode.SplatZones:
                    return "Splat Zones";
                case GameMode.Rainmaker:
                    return "Rainmaker";
                case GameMode.ClamBlitz:
                    return "Clam Blitz";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Emote GetModeImageLink()
        {
            switch (this.Mode)
            {
                case GameMode.TowerControl:
                    return Emote.Parse("<:TC:587708959138381835>");
                case GameMode.SplatZones:
                    return Emote.Parse("<:SZ:587708958962221071>");
                case GameMode.Rainmaker:
                    return Emote.Parse("<:RM:587708959142707270>");
                case GameMode.ClamBlitz:
                    return Emote.Parse("<:CB:587708958689722369>");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public EmbedBuilder GetEmbedBuilder(string title = null)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder
            {
                Title = title ?? $"{this.GetModeName()} on {this.MapName}"
            };

            embedBuilder.WithFooter(e =>
            {
                e.IconUrl = this.GetModeImageLink().Url;
                e.Text = this.GetModeName();
            });

            return embedBuilder;
        }

        public static GameMode GetModeFromAcronym(string acronym)
        {
            switch (acronym.ToLower())
            {
                case "cb":
                    return GameMode.ClamBlitz;
                case "rm":
                    return GameMode.Rainmaker;
                case "tc":
                    return GameMode.TowerControl;
                case "sz":
                    return GameMode.SplatZones;
                default:
                    Logger.Error($"Unable to convert gamemode acronym {acronym}!");
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
