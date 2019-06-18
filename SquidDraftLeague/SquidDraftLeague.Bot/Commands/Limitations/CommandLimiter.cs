using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public class CommandLimiter
    {
        /// <summary>
        /// Name of the group or command this limiter references.
        /// </summary>
        public string Name { get; set; }

        public ILimitation[] Limitations { get; set; }

        public async Task<bool> CheckAllLimitationsAsync(SocketCommandContext context)
        {
            UnconditionalLimitation unconditionalLimitation = this.Limitations.OfType<UnconditionalLimitation>().FirstOrDefault();

            if (unconditionalLimitation != null)
            {
                return await unconditionalLimitation.CheckLimitationAsync(context);
            }

            if (this.Limitations.OfType<ChannelLimitation>().Any() && 
                this.Limitations.OfType<ChannelLimitation>().All(e => !e.CheckLimitationAsync(context).Result))
            {
                return false;
            }

            if (this.Limitations.OfType<RoleLimitation>().Any() && 
                this.Limitations.OfType<RoleLimitation>().All(e => !e.CheckLimitationAsync(context).Result))
            {
                return false;
            }

            return !this.Limitations.OfType<TimeLimitation>().Any() || 
                   this.Limitations.OfType<TimeLimitation>().Any(e => e.CheckLimitationAsync(context).Result);
        }
    }
}
