using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SquidDraftLeague.Bot.Scheduling.Services;

namespace SquidDraftLeague.Bot.Scheduling
{
    public class TestTask : IScheduledTask
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string Schedule => "* * * * *";
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.Info("It works.");
        }
    }
}
