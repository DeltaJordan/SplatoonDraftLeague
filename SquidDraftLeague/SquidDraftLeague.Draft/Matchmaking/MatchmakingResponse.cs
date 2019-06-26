using System;

namespace SquidDraftLeague.Draft.Matchmaking
{
    public class MatchmakingResponse
    {
        public bool Success { get; }
        public string Message { get; }
        public Exception Exception { get; }

        protected MatchmakingResponse(bool success, string message, Exception exception)
        {
            this.Success = success;
            this.Message = message;
            this.Exception = exception;
        }
    }
}