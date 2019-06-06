using Discord.Commands;
using Discord.WebSocket;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class DiscordAPIExtensions
    {
        public static SocketGuildUser GetGuildUser(this ulong id, SocketCommandContext context)
        {
            if (context == null)
            {
                return Program.Client.GetGuild(570743985530863649).GetUser(id);
            }

            return context.Guild.GetUser(id);
        }
    }
}
