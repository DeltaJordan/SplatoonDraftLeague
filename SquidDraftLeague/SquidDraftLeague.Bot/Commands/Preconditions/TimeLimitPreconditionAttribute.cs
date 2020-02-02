using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class TimeLimitPreconditionAttribute
    {
        // TODO No need for this, but I'm pretty proud of it so I won't delete it yet.

        /*private readonly TimePeriod[] timePeriods;

        /// <summary>
        /// Limits command(s) to certain time period(s).
        /// </summary>
        /// <param name="hours">Must be in pairs of two; times formatted HH:mm where the first of the pair is the start time and the second is the end time.</param>
        public TimeLimitPreconditionAttribute(params string[] hours)
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

            await context.Channel.SendMessageAsync("Draft is currently closed.");
            return PreconditionResult.FromError("Draft is currently closed.");
        }*/
    }
}
