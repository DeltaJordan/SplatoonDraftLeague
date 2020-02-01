using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Color = System.Drawing.Color;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class DiscordAPIExtensions
    {
        public static string ToUserMention(this ulong id)
        {
            return $"<@{id}>";
        }

        public static EmbedBuilder WithColor(this EmbedBuilder builder, Color color)
        {
            return builder.WithColor(color.R, color.G, color.B);
        }
    }
}
