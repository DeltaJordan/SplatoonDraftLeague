using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using SquidDraftLeague.Draft;
using Color = System.Drawing.Color;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class SetExtensions
    {
        public static DiscordEmbedBuilder GetEmbedBuilder(this Set set)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            builder.WithTitle($"Set #({set.SetNumber})");

            List<string> alphaTeamInfo = new List<string>();
            foreach (SdlPlayer alphaTeamPlayer in set.AlphaTeam.Players)
            {
                string captainText = alphaTeamPlayer == set.AlphaTeam.Captain
                    ? " [Captain]"
                    : "";

                string roleText = alphaTeamPlayer.RoleOne == string.Empty ? string.Empty : $"[{alphaTeamPlayer.RoleOne}]";

                alphaTeamInfo.Add($"{alphaTeamPlayer.DiscordId.ToUserMention()} [{alphaTeamPlayer.PowerLevel:0.0}] {roleText} {captainText}");
            }

            builder.AddField("Alpha Team", string.Join('\n', alphaTeamInfo));

            List<string> bravoTeamInfo = new List<string>();
            foreach (SdlPlayer bravoTeamPlayer in set.BravoTeam.Players)
            {
                string captainText = bravoTeamPlayer == set.BravoTeam.Captain
                    ? " [Captain]"
                    : "";

                string roleText = bravoTeamPlayer.RoleOne == string.Empty ? string.Empty : $"[{bravoTeamPlayer.RoleOne}]";

                bravoTeamInfo.Add($"{bravoTeamPlayer.DiscordId.ToUserMention()} [{bravoTeamPlayer.PowerLevel:0.0}] {roleText} {captainText}");
            }

            builder.AddField("Bravo Team", string.Join('\n', bravoTeamInfo));

            if (set.DraftPlayers.Any())
            {
                builder.AddField("Players Awaiting Team", string.Join('\n',
                        set.DraftPlayers.Select(e => e.DiscordId.ToUserMention() + $" [{e.PowerLevel:0.0}] [{e.RoleOne}]")));
            }

            return builder;
        }

        public static DiscordEmbedBuilder GetScoreEmbedBuilder(this Set set, decimal pointsWinning, decimal pointsLosing)
        {
            return set.GetEmbedBuilder()
                .AddField(e =>
                {
                    e.Name = "Alpha Team's Score";
                    e.Value = set.AlphaTeam.Score;
                    e.IsInline = true;
                })
                .AddField(e =>
                {
                    e.Name = "Bravo Team's Score";
                    e.Value = set.BravoTeam.Score;
                    e.IsInline = true;
                })
                .AddField(e =>
                {
                    e.Name = "Points Gained/Lost";
                    e.Value = $"+{pointsWinning:0.0} -{pointsLosing:0.0}";
                });
        }

        public static DiscordEmbedBuilder GetFeedEmbedBuilder(this Set set, DiscordChannel context)
        {
            string winningText = set.Winning switch
            {
                Set.WinningTeam.Alpha => "in favor of ",
                Set.WinningTeam.Bravo => "in favor of ",
                Set.WinningTeam.Tie => "a ",
                _ => throw new ArgumentOutOfRangeException()
            };

            List<string> streams = (from player in set.AllPlayers.TakeWhile(player => context != null)
                select context.Guild.GetMemberAsync(player.DiscordId).Result
                into playerUser
                where playerUser.Presence.Game.StreamType == GameStreamType.Twitch
                select playerUser.Presence.Game.Url).ToList();

            return new DiscordEmbedBuilder()
                .WithTitle($"Set {set.SetNumber}")
                .WithDescription($"Match {set.MatchNum} of 7: {set.GetCurrentStage().MapName} {set.GetCurrentStage().Mode}\n" +
                                 $"The score is **{set.AlphaTeam.Score}-{set.BravoTeam.Score}** {winningText}**{set.Winning}**!")
                .WithColor(Color.Yellow)
                .AddField(x =>
                {
                    x.Name = "Alpha";
                    x.Value = string.Join("\n", set.AlphaTeam.Players.Select(e => e.DiscordId.ToUserMention()));
                    x.IsInline = true;
                })
                .AddField(x =>
                {
                    x.Name = "Bravo";
                    x.Value = string.Join("\n", set.BravoTeam.Players.Select(e => e.DiscordId.ToUserMention()));
                    x.IsInline = true;
                })
                .WithTimestamp(DateTime.Now);
        }
    }
}
