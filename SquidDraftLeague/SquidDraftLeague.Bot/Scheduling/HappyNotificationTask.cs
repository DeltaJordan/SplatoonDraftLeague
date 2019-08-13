using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SquidDraftLeague.Bot.Scheduling.Services;

namespace SquidDraftLeague.Bot.Scheduling
{
    public class HappyNotificationTask : IScheduledTask
    {
        public string Schedule => "0 20 * * *";
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Program.Client.GetGuild(570743985530863649).GetTextChannel(572536965833162753).SendMessageAsync(
                "Double points hour has begun! " +
                "Any sets started in the next hour will gain double points when winning. " +
                "Points lost are not affected.");
        }
    }
}
