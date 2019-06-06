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

        public static Set SetFromChannel(ulong channel)
        {
            switch (channel)
            {
                case 572542086260457474:
                    return SetModule.Sets[0];
                case 572542140949856278:
                    return SetModule.Sets[1];
                case 572542164316192777:
                    return SetModule.Sets[2];
                default:
                    return null;
            }
        }

        public static SocketTextChannel ChannelFromSet(int setNumber)
        {
            switch (setNumber)
            {
                case 1:
                    return (SocketTextChannel) Program.Client.GetChannel(572542086260457474);
                case 2:
                    return (SocketTextChannel) Program.Client.GetChannel(572542140949856278);
                case 3:
                    return (SocketTextChannel) Program.Client.GetChannel(572542164316192777);
                default:
                    return null;
            }
        }
    }
}
