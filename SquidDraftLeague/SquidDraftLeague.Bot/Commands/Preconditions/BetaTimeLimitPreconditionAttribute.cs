using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class BetaTimeLimitPreconditionAttribute : PreconditionAttribute
    {
        private readonly TimeSpan[] startTimes;
        private readonly TimeSpan[] endTimes;

        /// <summary>
        /// Limits command(s) to certain time period(s).
        /// </summary>
        /// <param name="hours">Must be in pairs of two; times formatted HH:mm where the first of the pair is the start time and the second is the end time.</param>
        public BetaTimeLimitPreconditionAttribute(params string[] hours)
        {
            List<TimeSpan> startList = new List<TimeSpan>();
            List<TimeSpan> endList = new List<TimeSpan>();

            for (int i = 0; i < hours.Length; i += 2)
            {
                startList.Add(TimeSpan.Parse(hours[i]));
                endList.Add(TimeSpan.Parse(hours[i + 1]));
            }

            this.startTimes = startList.ToArray();
            this.endTimes = endList.ToArray();
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            for (int i = 0; i < this.startTimes.Length; i++)
            {
                if (TimeBetween(DateTime.UtcNow, this.startTimes[i], this.endTimes[i]))
                {
                    return PreconditionResult.FromSuccess();
                }
            }

            await context.Channel.SendMessageAsync("Beta is currently closed.");
            return PreconditionResult.FromError("Beta is currently closed.");
        }

        private static bool TimeBetween(DateTime datetime, TimeSpan start, TimeSpan end)
        {
            // convert datetime to a TimeSpan
            TimeSpan now = datetime.TimeOfDay;
            // see if start comes before end
            if (start < end)
                return start <= now && now <= end;
            // start is after end, so do the inverse comparison
            return !(end < now && now < start);
        }
    }
}
