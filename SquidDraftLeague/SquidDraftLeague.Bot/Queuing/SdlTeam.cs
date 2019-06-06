using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Discord;
using SquidDraftLeague.Bot.AirTable;

namespace SquidDraftLeague.Bot.Queuing
{
    public class SdlTeam
    {
        public ReadOnlyCollection<SdlPlayer> Players => this.players.AsReadOnly();
        private readonly List<SdlPlayer> players = new List<SdlPlayer>();

        public int Score => this.OrderedMatchResults.Aggregate(0, (e, f) => e + f);

        public readonly List<int> OrderedMatchResults = new List<int>();

        public SdlPlayer Captain { get; private set; }

        public bool IsCaptain(IUser user)
        {
            return user.Id == this.Captain.DiscordId;
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
            this.OrderedMatchResults.Clear();
        }
    }
}
