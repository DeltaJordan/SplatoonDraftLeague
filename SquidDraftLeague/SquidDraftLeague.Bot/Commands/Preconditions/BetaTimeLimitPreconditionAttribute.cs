using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class BetaTimeLimitPreconditionAttribute : PreconditionAttribute
    {
        private readonly TimePeriod[] timePeriods;

        /// <summary>
        /// Limits command(s) to certain time period(s).
        /// </summary>
        /// <param name="hours">Must be in pairs of two; times formatted HH:mm where the first of the pair is the start time and the second is the end time.</param>
        public BetaTimeLimitPreconditionAttribute(params string[] hours)
        {
            List<TimePeriod> periodList = new List<TimePeriod>();

            for (int i = 0; i < hours.Length; i += 2)
            {
                periodList.Add(new TimePeriod(TimeSpan.Parse(hours[i]), TimeSpan.Parse(hours[i + 1])));
            }

            this.timePeriods = periodList.ToArray();
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            foreach (TimePeriod timePeriod in this.timePeriods)
            {
                if (timePeriod.IsWithinPeriod(DateTime.UtcNow))
                {
                    return PreconditionResult.FromSuccess();
                }
            }

            await context.Channel.SendMessageAsync("Beta is currently closed.");
            return PreconditionResult.FromError("Beta is currently closed.");
        }
    }
}
