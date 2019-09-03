using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class MoveToSetResponse : MatchmakingResponse
    {
        public Set Result { get; }

        public MoveToSetResponse(bool success, string message = null, Exception exception = null, Set result = null) 
            : base(success, message, exception)
        {
            this.Result = result;
        }
    }
}
