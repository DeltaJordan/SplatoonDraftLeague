using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using SquidDraftLeague.Draft;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class LobbyExtensions
    {
        public static EmbedBuilder GetEmbedBuilder(this Lobby lobby)
        {
            EmbedBuilder builder = new EmbedBuilder();

            builder.WithTitle($"Lobby #{lobby.LobbyNumber}");

            EmbedFieldBuilder powerLevelFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Power Level Range",
                Value = $"{lobby.LobbyPowerLevel - lobby.CurrentDelta} - {lobby.LobbyPowerLevel + lobby.CurrentDelta}",
                IsInline = true
            };
            builder.Fields.Add(powerLevelFieldBuilder);

            EmbedFieldBuilder classFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Class",
                Value = $"{lobby.Class}",
                IsInline = true
            };
            builder.Fields.Add(classFieldBuilder);

            EmbedFieldBuilder playersFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Players",
                Value = string.Join('\n', lobby.Players.Select(e => Program.Client.GetUser(e.DiscordId).Mention)),
                IsInline = false
            };
            builder.Fields.Add(playersFieldBuilder);

            builder.Footer = new EmbedFooterBuilder();

            if (!lobby.IsFull)
                builder.Footer.WithText(
                    $"Queue Time Remaining: {lobby.LastUpdate.Add(TimeSpan.FromMinutes(20)) - DateTime.Now:mm\\:ss}");

            return builder;
        }
    }
}
