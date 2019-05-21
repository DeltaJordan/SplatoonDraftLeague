using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AirtableApiClient;
using Discord;
using NLog;

namespace Plus0_Bot.AirTable
{
    public static class AirTableClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<SdlPlayer> RetrievePlayer(IGuildUser guildUser)
        {
            string offset = null;
            string errorMessage = null;
            List<AirtableRecord> records = new List<AirtableRecord>();

            using (AirtableBase airtableBase = new AirtableBase(Globals.BotSettings.AppKey, Globals.BotSettings.BaseId))
            {
                do
                {
                    Logger.Info($"Retrieving data with offset {offset}.");

                    Task<AirtableListRecordsResponse> task = airtableBase.ListRecords(
                        "Draft Standings",
                        offset,
                        null,
                        null,
                        null,
                        null
                        );

                    AirtableListRecordsResponse response = await task;

                    if (response.Success)
                    {
                        Logger.Info($"Success! Continuing with offset \"{response.Offset}\"");
                        records.AddRange(response.Records.ToList());
                        offset = response.Offset;
                    }
                    else if (response.AirtableApiError != null)
                    {
                        errorMessage = response.AirtableApiError.ErrorMessage;
                        break;
                    }
                    else
                    {
                        errorMessage = "Unknown error";
                        break;
                    }

                } while (offset != null);
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Logger.Error(errorMessage);
                return null;
            }

            Logger.Info("Searching for needed player id.");

            try
            {
                records = records.Where(e => e.Fields["DiscordID"].ToString() == guildUser.Id.ToString()).ToList();

                if (records.Count > 1)
                {
                    Logger.Warn($"There are more than one records in the Draft Standings table with the id {guildUser.Id}!");
                }
                else if (records.Count == 0)
                {
                    Logger.Warn($"There are no players registered with the discord id {guildUser.Id}!");
                    return null;
                }

                AirtableRecord playerRecord = records.First();

                SdlPlayer sdlPlayer = new SdlPlayer
                {
                    DiscordUser = guildUser,
                    PowerLevel = Convert.ToDouble(playerRecord.Fields["Power"].ToString())
                };

                return sdlPlayer;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }
    }
}
