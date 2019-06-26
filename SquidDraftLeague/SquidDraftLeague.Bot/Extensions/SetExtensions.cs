using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using SquidDraftLeague.Draft;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class SetExtensions
    {
        public static EmbedBuilder GetEmbedBuilder(this Set set)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle($"Set #({set.SetNumber})");

            List<string> alphaTeamInfo = new List<string>();
            foreach (SdlPlayer alphaTeamPlayer in set.AlphaTeam.Players)
            {
                string captainText = alphaTeamPlayer == set.AlphaTeam.Captain
                    ? " [Captain]"
                    : "";

                string roleText = alphaTeamPlayer.Role == string.Empty ? string.Empty : $"[{alphaTeamPlayer.Role}]";

                alphaTeamInfo.Add($"{alphaTeamPlayer.DiscordId.ToUserMention()} [{alphaTeamPlayer.PowerLevel:0.0}] {roleText} {captainText}");
            }

            EmbedFieldBuilder alphaTeamBuilder = new EmbedFieldBuilder
            {
                Name = "Alpha Team",
                Value = string.Join('\n', alphaTeamInfo)
            };

            builder.Fields.Add(alphaTeamBuilder);

            List<string> bravoTeamInfo = new List<string>();
            foreach (SdlPlayer bravoTeamPlayer in set.BravoTeam.Players)
            {
                string captainText = bravoTeamPlayer == set.BravoTeam.Captain
                    ? " [Captain]"
                    : "";

                string roleText = bravoTeamPlayer.Role == string.Empty ? string.Empty : $"[{bravoTeamPlayer.Role}]";

                bravoTeamInfo.Add($"{bravoTeamPlayer.DiscordId.ToUserMention()} [{bravoTeamPlayer.PowerLevel:0.0}] {roleText} {captainText}");
            }

            EmbedFieldBuilder bravoTeamBuilder = new EmbedFieldBuilder
            {
                Name = "Bravo Team",
                Value = string.Join('\n', bravoTeamInfo)
            };

            builder.Fields.Add(bravoTeamBuilder);

            if (set.DraftPlayers.Any())
            {
                EmbedFieldBuilder draftTeamBuilder = new EmbedFieldBuilder
                {
                    Name = "Players Awaiting Team",
                    Value = string.Join('\n',
                        set.DraftPlayers.Select(e => e.DiscordId.ToUserMention() + $"[{e.PowerLevel:0.0}] [{e.Role}]"))
                };

                builder.Fields.Add(draftTeamBuilder);
            }

            return builder;
        }
    }
}
