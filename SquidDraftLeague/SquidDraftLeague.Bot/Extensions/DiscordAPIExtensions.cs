using Discord.Commands;
using Discord.WebSocket;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class DiscordAPIExtensions
    {
        public static string ToUserMention(this ulong id)
        {
            return $"<@{id}>";
        }
    }
}
