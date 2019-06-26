using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using SquidDraftLeague.Draft.Map;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class StageExtensions
    {
        public static Emote GetModeImageLink(this Stage stage)
        {
            switch (stage.Mode)
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

        public static EmbedBuilder GetEmbedBuilder(this Stage stage, string title = null)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder
            {
                Title = title ?? $"{stage.GetModeName()} on {stage.MapName}"
            };

            embedBuilder.WithFooter(e =>
            {
                e.IconUrl = stage.GetModeImageLink().Url;
                e.Text = stage.GetModeName();
            });

            return embedBuilder;
        }
    }
}
