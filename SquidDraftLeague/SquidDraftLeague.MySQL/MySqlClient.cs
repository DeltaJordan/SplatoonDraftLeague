using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using Newtonsoft.Json.Linq;
using NLog;
using SquidDraftLeague.Draft;
using SquidDraftLeague.Draft.Map;
using SquidDraftLeague.MySQL.Entities;
using SquidDraftLeague.MySQL.Extensions;
using SquidDraftLeague.Settings;
using UniqueIdGenerator.Net;

namespace SquidDraftLeague.MySQL
{
    public static class MySqlClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly MySqlConnection MySqlConnection;

        static MySqlClient()
        {
            string connectionString =
                $"server={Globals.BotSettings.MySqlIp};user={Globals.BotSettings.MySqlUsername};database=sdl;port=3306;password={Globals.BotSettings.MySqlPassword}";

            MySqlConnection = new MySqlConnection(connectionString);
            MySqlConnection.Open();
        }

        public static async Task<ApiUser[]> GetApiUsers()
        {
            MySqlCommand selectCommand = new MySqlCommand("SELECT * FROM `Api Users`", MySqlConnection);

            List<ApiUser> users = new List<ApiUser>();

            using (MySqlDataReader executeReaderAsync = (MySqlDataReader) await selectCommand.ExecuteReaderAsync())
            {
                while (executeReaderAsync.Read())
                {
                    users.Add(new ApiUser
                    {
                        Id = executeReaderAsync["Id"].ToString(),
                        Role = executeReaderAsync["Role"].ToString(),
                        PasswordHash = executeReaderAsync["PasswordHash"].ToString(),
                        UserName = executeReaderAsync["Username"].ToString()
                    });
                }
            }

            return users.ToArray();
        }

        public static async Task<bool> CheckHasPlayedSet(SdlPlayer player)
        {
            MySqlCommand selectCommand =
                new MySqlCommand(
                    $"SELECT COUNT(*) " +
                    $"FROM `Draft Log` " +
                    $"WHERE `Alpha Players` LIKE '%{player.DiscordId}%' " +
                    $"OR `Bravo Players` LIKE '%{player.DiscordId}%'",
                    MySqlConnection);

            return Convert.ToInt32(await selectCommand.ExecuteScalarAsync()) > 0;
        }

        public static async Task<DateTime> GetDateOfLastSet(SdlPlayer player)
        {
            List<DateTime> setTimes = new List<DateTime>();

            MySqlCommand selectCommand =
                new MySqlCommand(
                    $"SELECT Date " +
                    $"FROM `Draft Log` " +
                    $"WHERE `Alpha Players` LIKE '%{player.DiscordId}%' " +
                    $"OR `Bravo Players` LIKE '%{player.DiscordId}%'",
                    MySqlConnection);

            using (MySqlDataReader reader = selectCommand.ExecuteReader())
            {
                while (await reader.ReadAsync())
                {
                    setTimes.Add((DateTime) reader["Date"]);
                }
            }

            return setTimes.OrderByDescending(x => x).FirstOrDefault();
        }

        public static async Task SetRoleAsync(SdlPlayer player, string role, int roleNum)
        {
            MySqlCommand updateRoleCommand = new MySqlCommand($"UPDATE Players SET `Role {roleNum}`=@Role WHERE `Discord ID`=@PlayerID", MySqlConnection);
            updateRoleCommand.Parameters.AddWithValue("@Role", role);
            updateRoleCommand.Parameters.AddWithValue("@PlayerID", player.DiscordId);
            await updateRoleCommand.ExecuteNonQueryAsync();
        }

        public static async Task SetFriendCodeAsync(SdlPlayer player, string code)
        {
            MySqlCommand updateRoleCommand = new MySqlCommand($"UPDATE Players SET `Friend Code`=@Code WHERE `Discord ID`=@PlayerID", MySqlConnection);
            updateRoleCommand.Parameters.AddWithValue("@Code", code);
            updateRoleCommand.Parameters.AddWithValue("@PlayerID", player.DiscordId);
            await updateRoleCommand.ExecuteNonQueryAsync();
        }

        public static async Task<(int Placement, string Ordinal)> GetPlayerStandings(SdlPlayer player)
        {
            // TODO placement was a bit broken before so I'll wait on this. Also might be team standing instead of player.
            return (1, GetOrdinal(1));
        }

        private static string GetOrdinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return "th";
            }

            switch (num % 10)
            {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }

        }

        public static async Task RegisterTeam(string name, string logoUrl, ulong captainId, Color? color = null)
        {
            Color teamColor = color ?? Color.Gray;

            // Snowflake generator.
            // Generator ID = May 5, 2015 the release date of Splatoon 1.
            Generator generator = new Generator(15285,
                new DateTime(2019, 6, 22, new GregorianCalendar(GregorianCalendarTypes.USEnglish)));

            ulong teamId = generator.NextLong();

            MySqlCommand insertCommand =
                new MySqlCommand(
                     "INSERT INTO " +
                     "Teams (`Team ID`,Name,`Logo URL`,Color,`Captain Discord ID`) " +
                    $"VALUES({teamId},@Name,@LogoUrl,#{teamColor.R:X2}{teamColor.G:X2}{teamColor.B:X2},{captainId})",
                    MySqlConnection);
            insertCommand.Parameters.AddWithValue("@Name", name);
            insertCommand.Parameters.AddWithValue("@LogoUrl", logoUrl);

            if (await insertCommand.ExecuteNonQueryAsync() > 0)
                return;

            throw new SdlMySqlException(SdlMySqlException.ExceptionType.ZeroUpdates,
                "Failed to register a team in the database; SQL responded with 0 updated rows.");
        }

        public static async Task AddPlayerToTeam(ulong teamId, SdlPlayer player)
        {
            if (player.TeamId != default)
            {
                throw new SdlMySqlException(SdlMySqlException.ExceptionType.DuplicateEntry,
                    "Failed to add player to a team in the database; This player is already on a team.");
            }

            MySqlCommand updateCommand =
                new MySqlCommand($"UPDATE Players SET `Active Team`={teamId}", MySqlConnection);

            if (await updateCommand.ExecuteNonQueryAsync() > 0)
                return;

            throw new SdlMySqlException(SdlMySqlException.ExceptionType.ZeroUpdates,
                "Failed to add player to a team in the database; SQL responded with 0 updated rows.");
        }

        public static async Task RegisterPlayer(ulong discordId, double startingPowerLevel, string nickname)
        {
            if (await RetrieveSdlPlayer(discordId) != null)
            {
                throw new SdlMySqlException(SdlMySqlException.ExceptionType.DuplicateEntry,
                    "Failed to insert new player into database; There is already a player with this Discord ID in the database.");
            }

            MySqlCommand insertCommand =
                new MySqlCommand(
                    $"INSERT INTO Players (`Discord ID`,Name,Power,`Starting Power`) " +
                    $"VALUES({discordId},@Nickname,{startingPowerLevel},{startingPowerLevel})",
                    MySqlConnection);
            insertCommand.Parameters.AddWithValue("@Nickname", nickname);

            if (await insertCommand.ExecuteNonQueryAsync() > 0)
                return;

            throw new SdlMySqlException(SdlMySqlException.ExceptionType.ZeroUpdates,
                "Failed to insert new player into database; SQL responded with 0 updated rows.");
        }

        public static async Task ReportTeamScores(Set set, decimal gain, decimal loss)
        {
            MySqlCommand insertCommand =
                new MySqlCommand($"INSERT INTO `Draft Log` " +
                                 $"(Date,`Alpha Team ID`,`Bravo Team ID`,`Alpha Players`,`Bravo Players`) " +
                                 $"VALUES(@Date,{set.AlphaTeam.Id},{set.BravoTeam.Id}," +
                                 $"'{string.Join(",", set.AlphaTeam.Players.Select(x => x.DiscordId.ToString()))}'," +
                                 $"'{string.Join(",", set.BravoTeam.Players.Select(x => x.DiscordId.ToString()))}')",
                    MySqlConnection);

            insertCommand.Parameters.AddWithValue("@Date", DateTime.UtcNow);

            if (await insertCommand.ExecuteNonQueryAsync() <= 0)
            {
                // TODO Throw exception that this didn't work.
            }

            // TODO Below values are not in columns yet.
            /*
                fields.AddField("Alpha Score", set.AlphaTeam.Score);
                fields.AddField("Bravo Score", set.BravoTeam.Score);
                fields.AddField("Gain", gain);
                fields.AddField("Loss", loss);
                fields.AddField("A SZ",
                    set.AlphaTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.SplatZones)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("B SZ",
                    set.BravoTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.SplatZones)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("A TC",
                    set.AlphaTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.TowerControl)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("B TC",
                    set.BravoTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.TowerControl)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("A RM",
                    set.AlphaTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.Rainmaker)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("B RM",
                    set.BravoTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.Rainmaker)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("A CB",
                    set.AlphaTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.ClamBlitz)
                        .Aggregate(0, (e, f) => e + f.Score));
                fields.AddField("B CB",
                    set.BravoTeam.OrderedMatchResults
                        .Select((e, index) => new { Score = e, set.Stages[index].Mode })
                        .Where(e => e.Score == 1 && e.Mode == GameMode.ClamBlitz)
                        .Aggregate(0, (e, f) => e + f.Score));
            */
        }

        public static async Task ReportPlayerScores(SdlPlayer player, decimal score)
        {
            MySqlCommand updateCommand = new MySqlCommand($"UPDATE Players SET `Power`={player.PowerLevel + score} WHERE `Discord ID`=@PlayerID", MySqlConnection);
            updateCommand.Parameters.AddWithValue("@PlayerID", player.DiscordId);

            if (await updateCommand.ExecuteNonQueryAsync() > 0)
                return;

            throw new SdlMySqlException(SdlMySqlException.ExceptionType.ZeroUpdates,
                "Failed to update player's score in the database; SQL responded with 0 updated rows.");
        }

        public static async Task<Stage[]> GetMapList()
        {
            List<Stage> resultStages = new List<Stage>();

            MySqlCommand selectCommand = new MySqlCommand($"SELECT * FROM `Map List`", MySqlConnection);

            using (MySqlDataReader dataReader = (MySqlDataReader) await selectCommand.ExecuteReaderAsync())
            {

                while (await dataReader.ReadAsync())
                {
                    string mapInfo = dataReader["Name"].ToString();
                    string mapName = mapInfo.Substring(0, mapInfo.Length - 3);
                    string modeInfo = mapInfo.Substring(mapInfo.Length - 2);

                    Stage currentStage = new Stage
                    {
                        MapName = mapName,
                        Mode = Stage.GetModeFromAcronym(modeInfo)
                    };

                    resultStages.Add(currentStage);
                }
            }

            return resultStages.ToArray();
        }

        public static async Task PenalizePlayer(SdlPlayer player, decimal points, string notes)
        {
            // TODO Need to log the notes somewhere as well.

            MySqlCommand updateCommand = new MySqlCommand($"UPDATE Players SET `Power`={player.PowerLevel - points} WHERE `Discord ID`=@PlayerID", MySqlConnection);
            updateCommand.Parameters.AddWithValue("@PlayerID", player.DiscordId);

            if (await updateCommand.ExecuteNonQueryAsync() > 0)
                return;

            throw new SdlMySqlException(SdlMySqlException.ExceptionType.ZeroUpdates,
                "Failed to apply the player's penalty to their power level in the database; SQL responded with 0 updated rows.");
        }

        public static async Task<SdlPlayer[]> RetrieveAllSdlPlayers()
        {
            try
            {
                List<SdlPlayer> allPlayers = new List<SdlPlayer>();

                MySqlCommand retrievePlayerCommand = new MySqlCommand("SELECT * FROM Players", MySqlConnection);
                using (MySqlDataReader retrievePlayerReader = (MySqlDataReader) await retrievePlayerCommand.ExecuteReaderAsync())
                {
                    while (await retrievePlayerReader.ReadAsync())
                    {
                        SdlPlayer sdlPlayer = new SdlPlayer(Convert.ToUInt64(retrievePlayerReader["Discord ID"]))
                        {
                            Nickname = retrievePlayerReader["Name"].ToString(),
                            PowerLevel = (decimal)retrievePlayerReader["Power"],
                            RoleOne = retrievePlayerReader["Role 1"] == DBNull.Value ?
                                retrievePlayerReader["Role 1"].ToString() :
                                string.Empty,
                            RoleTwo = retrievePlayerReader["Role 2"] == DBNull.Value ?
                                retrievePlayerReader["Role 2"].ToString() :
                                string.Empty
                        };

                        if (retrievePlayerReader.TryGetValue("Friend Code", out object friendCode))
                        {
                            sdlPlayer.SwitchFriendCode = friendCode.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Role 1", out object roleOne))
                        {
                            sdlPlayer.RoleOne = roleOne.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Role 2", out object roleTwo))
                        {
                            sdlPlayer.RoleTwo = roleTwo.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Active Team", out object activeTeam))
                        {
                            sdlPlayer.TeamId = Convert.ToUInt64(activeTeam);
                        }

                        // TODO Win Rates.

                        allPlayers.Add(sdlPlayer);
                    }
                }

                return allPlayers.ToArray();
            }
            catch (Exception e)
            {
                // TODO Catch the exception better.
                Logger.Error(e);
                throw;
            }
        }

        public static async Task<SdlPlayer> RetrieveSdlPlayer(ulong discordId)
        {
            try
            {
                MySqlCommand retrievePlayerCommand = new MySqlCommand("SELECT * FROM Players WHERE `Discord ID`=@PlayerID", MySqlConnection);
                retrievePlayerCommand.Parameters.AddWithValue("@PlayerID", discordId);
                using (MySqlDataReader retrievePlayerReader = (MySqlDataReader) await retrievePlayerCommand.ExecuteReaderAsync())
                {
                    while (await retrievePlayerReader.ReadAsync())
                    {
                        SdlPlayer sdlPlayer = new SdlPlayer(discordId)
                        {
                            Nickname = retrievePlayerReader["Name"].ToString(),
                            PowerLevel = (decimal) retrievePlayerReader["Power"],
                            RoleOne = retrievePlayerReader["Role 1"] == DBNull.Value ? 
                                retrievePlayerReader["Role 1"].ToString() :
                                string.Empty,
                            RoleTwo = retrievePlayerReader["Role 2"] == DBNull.Value ? 
                                retrievePlayerReader["Role 2"].ToString() :
                                string.Empty
                        };

                        if (retrievePlayerReader.TryGetValue("Friend Code", out object friendCode))
                        {
                            sdlPlayer.SwitchFriendCode = friendCode.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Role 1", out object roleOne))
                        {
                            sdlPlayer.RoleOne = roleOne.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Role 2", out object roleTwo))
                        {
                            sdlPlayer.RoleTwo = roleTwo.ToString();
                        }

                        if (retrievePlayerReader.TryGetValue("Active Team", out object activeTeam))
                        {
                            sdlPlayer.TeamId = Convert.ToUInt64(activeTeam);
                        }

                        // TODO Win Rates.

                        return sdlPlayer;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                // TODO Catch the exception better.
                Logger.Error(e);
                throw;
            }
        }
    }
}
