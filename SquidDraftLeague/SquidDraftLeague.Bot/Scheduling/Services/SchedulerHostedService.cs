using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NCrontab;
using NLog;

namespace SquidDraftLeague.Bot.Scheduling.Services
{
    public class SchedulerHostedService : HostedService
    {
        public event EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<SchedulerTaskWrapper> scheduledTasks = new List<SchedulerTaskWrapper>();

        public SchedulerHostedService(IEnumerable<IScheduledTask> scheduledTasks)
        {
            DateTime referenceTime = DateTime.UtcNow;

            foreach (IScheduledTask scheduledTask in scheduledTasks)
            {
                this.scheduledTasks.Add(new SchedulerTaskWrapper
                {
                    Schedule = CrontabSchedule.Parse(scheduledTask.Schedule),
                    Task = scheduledTask,
                    NextRunTime = referenceTime
                });
            }

            foreach (SchedulerTaskWrapper schedulerTaskWrapper in this.scheduledTasks)
            {
                schedulerTaskWrapper.Increment();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await this.ExecuteOnceAsync(cancellationToken);

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        private async Task ExecuteOnceAsync(CancellationToken cancellationToken)
        {
            TaskFactory taskFactory = new TaskFactory(TaskScheduler.Current);
            DateTime referenceTime = DateTime.UtcNow;

            List<SchedulerTaskWrapper> tasksThatShouldRun = this.scheduledTasks.Where(t => t.ShouldRun(referenceTime)).ToList();

            foreach (SchedulerTaskWrapper taskThatShouldRun in tasksThatShouldRun)
            {
                taskThatShouldRun.Increment();

                await taskFactory.StartNew(
                    async () => {
                        try
                        {
                            await taskThatShouldRun.Task.ExecuteAsync(cancellationToken);

                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                    },
                    cancellationToken);
            }
        }

        private class SchedulerTaskWrapper
        {
            public CrontabSchedule Schedule { get; set; }
            public IScheduledTask Task { get; set; }

            public DateTime LastRunTime { get; set; }
            public DateTime NextRunTime { get; set; }

            private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

            public void Increment()
            {
                this.LastRunTime = this.NextRunTime;
                this.NextRunTime = this.Schedule.GetNextOccurrence(this.NextRunTime);
                Logger.Info(
                    $"Next occurance for {this.Task.Schedule} is {this.NextRunTime}. It is currently {DateTime.UtcNow} and the last run time is {this.LastRunTime}.");
            }

            public bool ShouldRun(DateTime currentTime)
            {
                return this.NextRunTime < currentTime && this.LastRunTime != this.NextRunTime;
            }
        }
    }
}
