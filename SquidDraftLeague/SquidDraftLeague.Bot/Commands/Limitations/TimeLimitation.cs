using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public class TimeLimitation : ILimitation
    {
        public bool Inverse { get; set; }

        public TimePeriod[] TimePeriods { get; }

        public TimeLimitation(params TimePeriod[] timePeriods)
        {
            this.TimePeriods = timePeriods;
        }

        public async Task<bool> CheckLimitationAsync(SocketCommandContext context)
        {
            foreach (TimePeriod timePeriod in this.TimePeriods)
            {
                if (timePeriod.IsWithinPeriod(DateTime.UtcNow))
                {
                    return !this.Inverse;
                }
            }

            return this.Inverse;
        }
    }
}
