using System;
using System.Collections.Generic;
using System.Text;
using DSharpPlus.Entities;
using SquidDraftLeague.Draft.Map;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class StageExtensions
    {
        public static DiscordEmoji GetModeEmote(this Stage stage)
        {
            switch (stage.Mode)
            {
                case GameMode.TowerControl:
                    return DiscordEmoji.FromGuildEmote(Program.Client, 587708959138381835);
                case GameMode.SplatZones:
                    return DiscordEmoji.FromGuildEmote(Program.Client, 587708958962221071);
                case GameMode.Rainmaker:
                    return DiscordEmoji.FromGuildEmote(Program.Client, 587708959142707270);
                case GameMode.ClamBlitz:
                    return DiscordEmoji.FromGuildEmote(Program.Client, 587708958689722369);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static DiscordEmbedBuilder GetEmbedBuilder(this Stage stage, string title = null)
        {
            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder
            {
                Title = title ?? $"{stage.GetModeName()} on {stage.MapName}"
            };

            embedBuilder.WithFooter(stage.GetModeName(), $"https://cdn.discordapp.com/emojis/{stage.GetModeEmote().Id}.png?v=1");

            return embedBuilder;
        }
    }
}
