using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public class ChannelLimitation : ILimitation
    {
        public bool Inverse { get; set; }

        public ulong ChannelId { get; }

        public ChannelLimitation(ulong channelId)
        {
            this.ChannelId = channelId;
        }

        public async Task<bool> CheckLimitationAsync(SocketCommandContext context)
        {
            return !this.Inverse == (this.ChannelId == context.Channel.Id);
        }
    }
}
