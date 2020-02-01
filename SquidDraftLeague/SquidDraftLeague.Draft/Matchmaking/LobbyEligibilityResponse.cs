using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class LobbyEligibilityResponse : MatchmakingResponse
    {
        public LobbyEligibilityResponse(bool success, string message = null, Exception exception = null) 
            : base(success, message, exception)
        {
        }
    }
}
