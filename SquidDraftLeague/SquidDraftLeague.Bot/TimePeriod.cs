using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Bot
{
    public struct TimePeriod
    {
        public TimeSpan Start;
        public TimeSpan End;

        public TimePeriod(TimeSpan start, TimeSpan end)
        {
            this.Start = start;
            this.End = end;
        }

        public bool IsWithPeriod(DateTime datetime)
        {
            // convert datetime to a TimeSpan
            TimeSpan now = datetime.TimeOfDay;
            // see if start comes before end
            if (this.Start < this.End)
                return this.Start <= now && now <= this.End;
            // start is after end, so do the inverse comparison
            return !(this.End < now && now < this.Start);
        }
    }
}
