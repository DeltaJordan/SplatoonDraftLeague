using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace SquidDraftLeague.Draft
{
    public class SdlTeam
    {
        public string Name { get; }
        public ReadOnlyCollection<SdlPlayer> Players => this.players.AsReadOnly();
        private readonly List<SdlPlayer> players = new List<SdlPlayer>();

        public int Score => this.OrderedMatchResults.Aggregate(0, (e, f) => e + f);

        public readonly List<int> OrderedMatchResults = new List<int>();

        public SdlPlayer Captain { get; private set; }
        public ulong Id { get; set; }
        public string LogoUrl { get; set; }
        public Color Color { get; set; }

        public SdlTeam(string name)
        {
            this.Name = name;
        }

        public bool IsCaptain(ulong discordId)
        {
            return discordId == this.Captain?.DiscordId;
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
