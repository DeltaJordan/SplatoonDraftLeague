using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using SquidDraftLeague.Draft.Penalties;
using SquidDraftLeague.Language.Resources;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class CheckPenaltyAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            string penaltyDir = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Penalties")).FullName;
            string penaltyFile = Path.Combine(penaltyDir, $"{context.User.Id}.penalty");

            if (!File.Exists(penaltyFile))
            {
                return PreconditionResult.FromSuccess();
            }

            Record record = JsonConvert.DeserializeObject<Record>(File.ReadAllText(penaltyFile));

            switch (record.AllInfractions.Count)
            {
                case 0:
                case 1:
                    return PreconditionResult.FromSuccess();
                case 2:
                    if (record.AllInfractions.Any(e => e.TimeOfOffense > DateTime.Now - TimeSpan.FromDays(1)))
                    {
                        await context.Channel.SendMessageAsync(Resources.BanMessage + 
                            $"{record.AllInfractions.OrderByDescending(e => e.TimeOfOffense).First().TimeOfOffense + TimeSpan.FromDays(1) - DateTime.Now:hh\\:mm} (Hours:Minutes)");

                        return PreconditionResult.FromError("User is currently banned.");
                    }
                    else
                    {
                        return PreconditionResult.FromSuccess();
                    }
                default:
                    if (record.AllInfractions.Any(e => e.TimeOfOffense > DateTime.Now - TimeSpan.FromDays(7)))
                    {
                        await context.Channel.SendMessageAsync(Resources.BanMessage +
                                                         $"{record.AllInfractions.OrderByDescending(e => e.TimeOfOffense).First().TimeOfOffense + TimeSpan.FromDays(7) - DateTime.Now:dd\\.hh\\:mm} (Days.Hours:Minutes)");

                        return PreconditionResult.FromError("User is currently banned.");
                    }
                    else
                    {
                        return PreconditionResult.FromSuccess();
                    }
            }
        }
    }
}
