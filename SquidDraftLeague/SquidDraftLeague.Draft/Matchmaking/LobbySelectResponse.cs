using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class LobbySelectResponse : MatchmakingResponse
    {
        public Lobby Result { get; }

        public LobbySelectResponse(bool success, string message = null, Exception exception = null, Lobby result = null) 
            : base(success, message, exception)
        {
            this.Result = result;
        }
    }
}
