using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Discord;
using Plus0_Bot.AirTable;

namespace Plus0_Bot.Queuing
{
    public class SdlTeam
    {
        public ReadOnlyCollection<SdlPlayer> Players => this.players.AsReadOnly();
        private readonly List<SdlPlayer> players = new List<SdlPlayer>();

        public SdlPlayer Captain { get; private set; }

        public bool IsCaptain(IUser user)
        {
            return user.Id == this.Captain.DiscordUser.Id;
        }

        public void AddPlayer(SdlPlayer player, bool asCaptain = false)
        {
            this.players.Add(player);

            if (asCaptain)
            {
                this.Captain = player;
            }
        }

        public void RemovePlayer(SdlPlayer player)
        {
            this.players.Remove(player);

            if (this.Captain.Equals(player))
            {
                this.Captain = this.players.OrderByDescending(e => e.PowerLevel).First();
            }
        }

        public void Clear()
        {
            this.players.Clear();
            this.Captain = null;
        }
    }
}
