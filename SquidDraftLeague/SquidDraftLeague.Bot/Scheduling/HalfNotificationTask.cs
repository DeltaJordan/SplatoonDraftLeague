using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SquidDraftLeague.Bot.Scheduling.Services;

namespace SquidDraftLeague.Bot.Scheduling
{
    public class HalfNotificationTask : IScheduledTask
    {
        public string Schedule => "0 1 * * *";
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Program.Client.GetGuild(570743985530863649).GetTextChannel(572536965833162753).SendMessageAsync(
                "Half points hour has begun! " +
                "Any sets started in the next hour will lose half points when losing. " +
                "Points won are not affected.");
        }
    }
}
