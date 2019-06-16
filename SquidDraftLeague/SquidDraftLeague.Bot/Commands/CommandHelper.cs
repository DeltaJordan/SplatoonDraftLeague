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
            // Alpha (4)
            584782819868409876,
            // Alpha (5)
            584782827846107138,
            // Bravo (1)
            572538738031460372,
            // Bravo (2)
            572538838057091097,
            // Bravo (3)
            572538838551887875,
            // Bravo (4)
            584782828844089398,
            // Bravo (5)
            584782829800521728,
            // In Set (1)
            572537995295457291,
            // In Set (2)
            572538539510726657,
            // In Set (3)
            572538622637506591,
            // In Set (4)
            584782498731524298,
            // In Set (5)
            584782557426745366
        };

        public static readonly ulong[] SetChannelIds =
        {
            572542086260457474,
            572542140949856278,
            572542164316192777,
            589955282365317151,
            589955337256173571
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
