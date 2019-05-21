using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Discord;
using Plus0_Bot.AirTable;

namespace Plus0_Bot.Queuing
{
    public class Set
    {
        public int SetNumber { get; }

        // TODO Move Captains to Team classes.
        public SdlPlayer AlphaCaptain { get; private set; }
        public SdlPlayer BravoCaptain { get; private set; }

        public IEnumerable<SdlPlayer> AllPlayers => this.AlphaTeam.Concat(this.BravoTeam).Concat(this.DraftPlayers);

        // TODO Use classes for teams with a public ReadOnlyCollection and a private List.
        public readonly List<SdlPlayer> AlphaTeam = new List<SdlPlayer>();
        public readonly List<SdlPlayer> BravoTeam = new List<SdlPlayer>();

        // TODO Not for draft players though.
        public readonly List<SdlPlayer> DraftPlayers = new List<SdlPlayer>();

        public bool AlphaPicking;

        public Set(int setNumber)
        {
            this.SetNumber = setNumber;
        }

        public void MoveLobbyToSet(Lobby lobby)
        {
            List<SdlPlayer> orderedPlayers = lobby.Players.OrderByDescending(e => e.PowerLevel).ToList();
            this.AlphaCaptain = orderedPlayers[0];
            this.BravoCaptain = orderedPlayers[1];

            this.AlphaTeam.Add(this.AlphaCaptain);
            this.BravoTeam.Add(this.BravoCaptain);

            this.DraftPlayers.AddRange(orderedPlayers.Skip(2));
        }

        public EmbedBuilder GetEmbedBuilder()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle($"Set #({this.SetNumber})");

            List<string> alphaTeamInfo = this.BravoTeam.Select(e => e.DiscordUser.Mention).ToList();
            alphaTeamInfo[this.AlphaTeam.IndexOf(this.AlphaCaptain)] = this.AlphaCaptain.DiscordUser.Mention + " [Captain]";

            EmbedFieldBuilder alphaTeamBuilder = new EmbedFieldBuilder
            {
                Name = "Alpha Team",
                Value = string.Join('\n', this.AlphaTeam.Select(e => e.DiscordUser.Mention))
            };

            builder.Fields.Add(alphaTeamBuilder);

            List<string> bravoTeamInfo = this.BravoTeam.Select(e => e.DiscordUser.Mention).ToList();
            bravoTeamInfo[this.BravoTeam.IndexOf(this.BravoCaptain)] = this.BravoCaptain.DiscordUser.Mention + " [Captain]";

            EmbedFieldBuilder bravoTeamBuilder = new EmbedFieldBuilder
            {
                Name = "Bravo Team",
                Value = string.Join('\n', bravoTeamInfo)
            };

            builder.Fields.Add(bravoTeamBuilder);

            if (this.DraftPlayers.Any())
            {
                EmbedFieldBuilder draftTeamBuilder = new EmbedFieldBuilder
                {
                    Name = "Players Awaiting Team",
                    Value = string.Join('\n', this.DraftPlayers.Select(e => e.DiscordUser.Mention))
                };

                builder.Fields.Add(draftTeamBuilder);
            }

            return builder;
        }
    }
}
