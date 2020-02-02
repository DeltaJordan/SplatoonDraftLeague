using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSharpPlus.Entities;
using SquidDraftLeague.Bot.Extensions.Entities;
using SquidDraftLeague.Draft;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class LobbyExtensions
    {
        public static DiscordEmbedBuilder GetEmbedBuilder(this Lobby lobby)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();

            builder.WithTitle($"Lobby #{lobby.LobbyNumber}");

            DiscordFieldBuilder powerLevelFieldBuilder = new DiscordFieldBuilder
            {
                Name = "Power Level Range",
                Value = $"{lobby.LobbyPowerLevel - lobby.CurrentDelta} - {lobby.LobbyPowerLevel + lobby.CurrentDelta}",
                IsInline = true
            };
            builder.AddField(powerLevelFieldBuilder);

            DiscordFieldBuilder classFieldBuilder = new DiscordFieldBuilder
            {
                Name = "Class",
                Value = $"{lobby.Class}",
                IsInline = true
            };
            builder.AddField(classFieldBuilder);

            DiscordFieldBuilder playersFieldBuilder = new DiscordFieldBuilder
            {
                Name = "Players",
                Value = string.Join('\n', lobby.Players.Select(e => (Program.Client.GetUserAsync(e.DiscordId).Result).Mention)),
                IsInline = false
            };
            builder.AddField(playersFieldBuilder);

            builder.Footer = new DiscordEmbedBuilder.EmbedFooter();

            if (!lobby.IsFull)
                builder.Footer.Text = 
                    $"Queue Time Remaining: {lobby.LastUpdate.Add(TimeSpan.FromMinutes(20)) - DateTime.Now:mm\\:ss}";

            return builder;
        }
    }
}
