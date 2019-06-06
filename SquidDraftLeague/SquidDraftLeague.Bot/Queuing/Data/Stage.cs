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

        public string GetModeImageLink()
        {
            string modeEscaped;

            switch (this.Mode)
            {
                case GameMode.TowerControl:
                    modeEscaped = "Tower_Control";
                    break;
                case GameMode.SplatZones:
                    modeEscaped = "Splat_Zones";
                    break;
                case GameMode.Rainmaker:
                    modeEscaped = "Rainmaker";
                    break;
                case GameMode.ClamBlitz:
                    modeEscaped = "Clam_Blitz";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            WebClient client = new WebClient();
            HtmlDocument document = new HtmlDocument();
            document.Load(client.OpenRead($"https://splatoonwiki.org/wiki/File:Mode_Icon_{modeEscaped}.png"));
            HtmlNode selectNode = document.DocumentNode.SelectSingleNode("//a[contains(@class, 'internal')]");
            string imageUrl = "https:" + selectNode.Attributes["href"].Value;
            return imageUrl;
        }

        public EmbedBuilder GetEmbedBuilder(string title = null)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder
            {
                Title = title ?? $"{this.GetModeName()} on {this.MapName}",
                ImageUrl = this.GetMapImageLink()
            };

            embedBuilder.WithFooter(e =>
            {
                e.IconUrl = this.GetModeImageLink();
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
