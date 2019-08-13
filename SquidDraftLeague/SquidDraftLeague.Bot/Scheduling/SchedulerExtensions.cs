using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SquidDraftLeague.Bot.Scheduling.Services;

namespace SquidDraftLeague.Bot.Scheduling
{
    public static class SchedulerExtensions
    {
        public static IServiceCollection AddScheduler(this IServiceCollection services)
        {
            return services.AddSingleton<IHostedService, SchedulerHostedService>();
        }

        public static IServiceCollection AddScheduler(this IServiceCollection services, EventHandler<UnobservedTaskExceptionEventArgs> unobservedTaskExceptionHandler)
        {
            return services.AddSingleton<IHostedService, SchedulerHostedService>(serviceProvider =>
            {
                SchedulerHostedService instance = new SchedulerHostedService(
                    serviceProvider.GetServices<IScheduledTask>(),
                    serviceProvider.GetRequiredService<IServiceScopeFactory>());
                instance.UnobservedTaskException += unobservedTaskExceptionHandler;
                return instance;
            });
        }
    }
}