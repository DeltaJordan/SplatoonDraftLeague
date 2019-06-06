using System;
using System.Linq;
using Discord;
using Discord.WebSocket;
using SquidDraftLeague.Bot.Queuing;

namespace SquidDraftLeague.Bot.Commands
{
    public static class CommandHelper
    {
        public static readonly ulong[] DraftRoleIds =
        {
            // Alpha (1)
            572538698575380491,
            // Alpha (2)
            572538824157298703,
            // Alpha (3)
            572538836002013185,
            // Bravo (1)
            572538738031460372,
            // Bravo (2)
            572538838057091097,
            // Bravo (3)
            572538838551887875,
            // In Set (1)
            572537995295457291,
            // In Set (2)
            572538539510726657,
            // In Set (3)
            572538622637506591
        };

        public static readonly ulong[] SetChannelIds =
        {
            572542086260457474,
            572542140949856278,
            572542164316192777
        };

        public static Set SetFromChannel(ulong channel)
        {
            return SetChannelIds.Contains(channel) ? SetModule.Sets[Array.IndexOf(SetChannelIds, channel)] : null;
        }

        public static SocketTextChannel ChannelFromSet(int setNumber)
        {
            if (setNumber > SetChannelIds.Length)
                return null;

            return (SocketTextChannel) Program.Client.GetChannel(SetChannelIds[setNumber - 1]);
        }
    }
}
