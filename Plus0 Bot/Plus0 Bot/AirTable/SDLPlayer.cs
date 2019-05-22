using System;
using System.Collections.Generic;
using System.Text;
using Discord;

namespace Plus0_Bot.AirTable
{
    public class SdlPlayer : IEquatable<SdlPlayer>
    {
        public IGuildUser DiscordUser { get; }
        public double PowerLevel { get; set; }

        public SdlPlayer(IGuildUser user)
        {
            this.DiscordUser = user;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SdlPlayer player))
                return base.Equals(obj);

            return player.DiscordUser.Id == this.DiscordUser.Id;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.DiscordUser != null ? this.DiscordUser.GetHashCode() : 0) * 397) ^ this.PowerLevel.GetHashCode();
            }
        }

        public bool Equals(SdlPlayer other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.DiscordUser.Id == other.DiscordUser.Id;
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
