using System;
using System.Collections.Generic;
using System.Text;
using Discord;

namespace Plus0_Bot.AirTable
{
    public class SdlPlayer
    {
        public IGuildUser DiscordUser { get; set; }
        public double PowerLevel { get; set; }

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
    }
}
