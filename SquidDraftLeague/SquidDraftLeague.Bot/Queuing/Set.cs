using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Commands;
using SquidDraftLeague.Bot.AirTable;
using SquidDraftLeague.Bot.Commands;
using SquidDraftLeague.Bot.Extensions;
using SquidDraftLeague.Bot.Queuing.Data;

namespace SquidDraftLeague.Bot.Queuing
{
    public class Set
    {
        public int SetNumber { get; }

        public IEnumerable<SdlPlayer> AllPlayers => this.AlphaTeam.Players.Concat(this.BravoTeam.Players).Concat(this.DraftPlayers);

        public SdlTeam AlphaTeam { get; }
        public SdlTeam BravoTeam { get; }
        
        public readonly List<SdlPlayer> DraftPlayers = new List<SdlPlayer>();

        public bool AlphaPicking;

        public int MatchNum;
        public List<Stage> PlayedStages = new List<Stage>();

        public int ResolveMode = 0;
        public bool Locked { get; set; }

        public event EventHandler<Set> Closed;

        private Timer draftTimer;
        private ITextChannel timeoutContext;

        private GameMode[] modeOrder = null;

        public Set(int setNumber)
        {
            this.SetNumber = setNumber;

            this.AlphaTeam = new SdlTeam();
            this.BravoTeam = new SdlTeam();

            this.draftTimer = new Timer(120000) {AutoReset = false};
            this.draftTimer.Elapsed += this.DraftTimer_Elapsed;
        }

        private async void DraftTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!this.DraftPlayers.Any())
            {
                this.draftTimer.Stop();
                this.timeoutContext = null;
                return;
            }

            await this.timeoutContext.SendMessageAsync("Choosing team member due to timeout.");

            await SetModule.PickPlayer(this, this.DraftPlayers[0].DiscordId.GetGuildUser(null), this.timeoutContext);
        }

        public void ReportScore(string winner)
        {
            winner = winner.ToLower();

            if (winner == "alpha")
            {
                this.AlphaTeam.OrderedMatchResults.Add(1);
                this.BravoTeam.OrderedMatchResults.Add(0);
            }
            else
            {
                this.AlphaTeam.OrderedMatchResults.Add(0);
                this.BravoTeam.OrderedMatchResults.Add(1);
            }
        }

        public void SetupTimeout(ITextChannel context)
        {
            this.timeoutContext = context;
            this.draftTimer.Start();
        }

        public void ResetTimeout()
        {
            this.draftTimer.Stop();
            this.draftTimer.Start();
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
            this.PlayedStages.Clear();
            this.MatchNum = 0;

            this.modeOrder = null;

            this.Closed?.Invoke(null, this);
        }

        public async Task<Stage> PickStage()
        {
            if (this.modeOrder == null)
            {
                this.modeOrder = Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Shuffle().ToArray();
            }

            List<Stage> stages = (await AirTableClient.GetMapList())
                .Where(e => !(this.PlayedStages.Any(f => f.MapName == e.MapName) && e.Mode == this.modeOrder[(this.MatchNum - 1) % 4]))
                .ToList();

            return stages[Globals.Random.Next(0, stages.Count - 1)];
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

                alphaTeamInfo.Add($"{alphaTeamPlayer.DiscordId.GetGuildUser(null).Mention}{captainText}");
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

                bravoTeamInfo.Add($"{bravoTeamPlayer.DiscordId.GetGuildUser(null).Mention}{captainText}");
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
                    Value = string.Join('\n', this.DraftPlayers.Select(e => e.DiscordId.GetGuildUser(null).Mention))
                };

                builder.Fields.Add(draftTeamBuilder);
            }

            return builder;
        }
    }
}
