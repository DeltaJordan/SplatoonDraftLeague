using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
using NLog;

namespace SquidDraftLeague.MySQL.Extensions
{
    public static class MySqlExtensions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool TryGetValue(this MySqlDataReader reader, string columnName, out object value)
        {
            value = null;

            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (!reader.IsDBNull(ordinal))
                {
                    value = reader[ordinal];
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            return value == null;
        }
    }
}
