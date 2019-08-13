using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NCrontab;

namespace SquidDraftLeague.Bot.Scheduling.Services
{
    public class SchedulerHostedService : HostedService
    {
        public event EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException;

        private readonly List<SchedulerTaskWrapper> scheduledTasks = new List<SchedulerTaskWrapper>();
        private readonly IServiceScopeFactory serviceScopeFactory;

        public SchedulerHostedService(IEnumerable<IScheduledTask> scheduledTasks, IServiceScopeFactory serviceScopeFactory)
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

            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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
                            using (IServiceScope scope = this.serviceScopeFactory.CreateScope())
                            {
                                Type t = taskThatShouldRun.Task.GetType();
                                MethodInfo method = t.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
                                object[] arguments = method?.GetParameters()
                                                .Select(a => a.ParameterType == typeof(CancellationToken) ? cancellationToken : scope.ServiceProvider.GetService(a.ParameterType))
                                                .ToArray();



                                //invoke.
                                if (typeof(Task) == method?.ReturnType)
                                {
                                    await (Task)method.Invoke(taskThatShouldRun.Task, arguments);
                                }
                                else
                                {
                                    method?.Invoke(taskThatShouldRun.Task, arguments);
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            UnobservedTaskExceptionEventArgs args = new UnobservedTaskExceptionEventArgs(
                                ex as AggregateException ?? new AggregateException(ex));

                            this.UnobservedTaskException?.Invoke(this, args);

                            if (!args.Observed)
                            {
                                throw;
                            }
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

            public void Increment()
            {
                this.LastRunTime = this.NextRunTime;
                this.NextRunTime = this.Schedule.GetNextOccurrence(this.NextRunTime);
            }

            public bool ShouldRun(DateTime currentTime)
            {
                return this.NextRunTime < currentTime && this.LastRunTime != this.NextRunTime;
            }
        }
    }
}
