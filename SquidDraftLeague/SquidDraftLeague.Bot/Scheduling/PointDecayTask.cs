using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using SquidDraftLeague.AirTable;
using SquidDraftLeague.Bot.Scheduling.Services;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Scheduling
{
    public class PointDecayTask : IScheduledTask
    {
        public string Schedule => "0 16 * * WED";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            SdlPlayer[] allSdlPlayers = await AirTableClient.RetrieveAllSdlPlayers();
            string activityDirectory = Directory.CreateDirectory(Path.Combine(Globals.AppPath, "Player Activity")).FullName;

            foreach (SdlPlayer sdlPlayer in allSdlPlayers)
            {
                try
                {
                    PlayerActivity playerActivity;
                    string playerFile = Path.Combine(activityDirectory, $"{sdlPlayer.DiscordId}.json");

                    if (File.Exists(playerFile))
                    {
                        playerActivity =
                            JsonConvert.DeserializeObject<PlayerActivity>(await File.ReadAllTextAsync(playerFile));
                    }
                    else
                    {
                        playerActivity = new PlayerActivity
                        {
                            PlayedSets = new List<DateTime>(),
                            Timeouts = new List<DateTime>()
                        };
                    }

                    int timeouts = playerActivity.Timeouts.Select(e => e.Date).Distinct()
                        .Count(e => e.Date > DateTime.UtcNow.Date - TimeSpan.FromDays(7));

                    timeouts = Math.Min(7, timeouts);

                    DateTime lastSetDateTime = await AirTableClient.GetDateOfLastSet(sdlPlayer);
                    int inactiveWeeks = (DateTime.UtcNow - lastSetDateTime).Days / 7;

                    if (inactiveWeeks == 0)
                        continue;

                    // Constrain to 1-4 weeks
                    inactiveWeeks = Math.Min(Math.Max(inactiveWeeks, 1), 4);

                    double decay = (7 - timeouts) * Math.Pow(15D * Math.Pow(sdlPlayer.PowerLevel, 2) / 4840000,
                                       Math.Pow(1.2, inactiveWeeks - 1)) / 7;

                    await AirTableClient.PenalizePlayer(sdlPlayer.DiscordId, decay, "Point decay.");
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }
    }
}
