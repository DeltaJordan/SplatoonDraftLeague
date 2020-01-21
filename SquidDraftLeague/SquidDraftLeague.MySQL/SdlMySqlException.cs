using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.MySQL
{
    public class SdlMySqlException : Exception
    {
        public ExceptionType Type { get; }

        public enum ExceptionType
        {
            ZeroUpdates,
            DuplicateEntry
        }

        public SdlMySqlException(ExceptionType type, string message) : base(message)
        {
            this.Type = type;
        }
    }
}
