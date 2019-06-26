using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class PickPlayerResponse : MatchmakingResponse
    {
        public bool LastPlayer { get; internal set; }

        public PickPlayerResponse(bool success, string message = null, Exception exception = null) 
            : base(success, message, exception)
        {
        }
    }
}
