using System;
using System.Collections.Generic;
using Discord;
using SquidDraftLeague.Bot.Queuing.Data;

namespace SquidDraftLeague.Bot.AirTable
{
    public class SdlPlayer : IEquatable<SdlPlayer>
    {
        public string AirtableId { get; set; }
        public string AirtableName { get; set; }
        public ulong DiscordId { get; }
        public double PowerLevel { get; set; }
        public string SwitchFriendCode { get; set; }
        public string Role { get; set; }

        public readonly Dictionary<GameMode, double> WinRates = new Dictionary<GameMode, double>();

        public double OverallWinRate { get; set; }

        public SdlPlayer(IGuildUser user)
        {
            this.DiscordId = user.Id;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SdlPlayer player))
                return obj.Equals(this);

            return player.DiscordId == this.DiscordId;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.DiscordId.GetHashCode() * 397) ^ this.PowerLevel.GetHashCode();
            }
        }

        public bool Equals(SdlPlayer other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.DiscordId == other.DiscordId;
        }

        public static bool operator ==(SdlPlayer playerOne, SdlPlayer playerTwo)
        {
            return playerOne?.Equals(playerTwo) ?? playerTwo is null;
        }

        public static bool operator !=(SdlPlayer playerOne, SdlPlayer playerTwo)
        {
            return !(playerOne == playerTwo);
        }
    }
}
