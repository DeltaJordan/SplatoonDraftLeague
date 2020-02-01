using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class SelectHostResponse : MatchmakingResponse
    {
        public ulong? DiscordId { get; }

        public SelectHostResponse(bool success, string message = null, Exception exception = null, ulong? discordId = null) : 
            base(success, message, exception)
        {
            this.DiscordId = discordId;
        }
    }
}
