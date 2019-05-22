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

        public IEnumerable<SdlPlayer> AllPlayers => this.AlphaTeam.Players.Concat(this.BravoTeam.Players).Concat(this.DraftPlayers);

        public SdlTeam AlphaTeam { get; }
        public SdlTeam BravoTeam { get; }
        
        public readonly List<SdlPlayer> DraftPlayers = new List<SdlPlayer>();

        public bool AlphaPicking;

        public Set(int setNumber)
        {
            this.SetNumber = setNumber;

            this.AlphaTeam = new SdlTeam();
            this.BravoTeam = new SdlTeam();
        }

        public void MoveLobbyToSet(Lobby lobby)
        {
            List<SdlPlayer> orderedPlayers = lobby.Players.OrderByDescending(e => e.PowerLevel).ToList();

            this.AlphaTeam.AddPlayer(orderedPlayers[0], true);
            this.BravoTeam.AddPlayer(orderedPlayers[1], true);

            this.DraftPlayers.AddRange(orderedPlayers.Skip(2));
        }

        public void Close()
        {
            this.AlphaTeam.Clear();
            this.BravoTeam.Clear();
            this.AlphaPicking = false;
            this.DraftPlayers.Clear();
        }

        public EmbedBuilder GetEmbedBuilder()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle($"Set #({this.SetNumber})");

            List<string> alphaTeamInfo = new List<string>();
            foreach (SdlPlayer alphaTeamPlayer in this.AlphaTeam.Players)
            {
                string captainText = alphaTeamPlayer == this.AlphaTeam.Captain
                    ? " [Captain]"
                    : "";

                alphaTeamInfo.Add($"{alphaTeamPlayer.DiscordUser.Mention}{captainText}");
            }

            EmbedFieldBuilder alphaTeamBuilder = new EmbedFieldBuilder
            {
                Name = "Alpha Team",
                Value = string.Join('\n', alphaTeamInfo)
            };

            builder.Fields.Add(alphaTeamBuilder);

            List<string> bravoTeamInfo = new List<string>();
            foreach (SdlPlayer bravoTeamPlayer in this.BravoTeam.Players)
            {
                string captainText = bravoTeamPlayer == this.BravoTeam.Captain
                    ? " [Captain]"
                    : "";

                bravoTeamInfo.Add($"{bravoTeamPlayer.DiscordUser.Mention}{captainText}");
            }

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
