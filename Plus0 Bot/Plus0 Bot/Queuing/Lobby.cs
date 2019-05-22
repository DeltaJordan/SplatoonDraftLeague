using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Timers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Plus0_Bot.AirTable;

namespace Plus0_Bot.Queuing
{
    public class Lobby
    {
        public bool IsFull => this.Players.Count == 8;
        public DateTime LastUpdate { get; set; }
        public double LobbyPowerLevel
        {
            get
            {
                if (!this.players.Any())
                {
                    return 0;
                }

                return Math.Round(this.players.Select(e => e.PowerLevel).Average(), 2);
            }
        }

        public int CurrentDelta { get; private set; }
        public int LobbyNumber { get; }
        public ReadOnlyCollection<SdlPlayer> Players => this.players.AsReadOnly();

        private SocketCommandContext commandContext;
        private Timer timer;

        private readonly List<SdlPlayer> players = new List<SdlPlayer>();

        public Lobby(int lobbyNumber)
        {
            this.LastUpdate = DateTime.Now;

            this.LobbyNumber = lobbyNumber;
            this.CurrentDelta = 25;

            this.timer = new Timer {Interval = 300000, AutoReset = true};
            this.timer.Elapsed += this.Timer_Elapsed;
        }

        public void Close()
        {
            this.players.Clear();
            this.timer.Stop();
            this.CurrentDelta = 25;
        }

        public void RenewContext(SocketCommandContext context)
        {
            this.commandContext = context;
        }

        public bool IsWithinThreshold(double power)
        {
            if (!this.players.Any())
            {
                return true;
            }

            double min = this.LobbyPowerLevel - this.CurrentDelta;
            double max = this.LobbyPowerLevel + this.CurrentDelta;

            return power > min && power < max;
        }

        public bool AddPlayer(SdlPlayer player, bool force = false)
        {
            if (!force && (this.IsFull || this.players.Any(e => e.DiscordUser.Id == player.DiscordUser.Id)))
            {
                return false;
            }

            this.players.Add(player);

            this.LastUpdate = DateTime.Now;
            this.timer.Stop();
            this.timer.Start();

            return true;
        }

        public void RemovePlayer(SdlPlayer player)
        {
            if (this.players.Contains(player))
            {
                this.players.Remove(player);
            }

            if (this.players.Count == 0)
            {
                this.Close();
            }
            else if (!this.IsFull && !this.timer.Enabled)
            {
                this.timer.Start();
            }
        }

        public EmbedBuilder GetEmbedBuilder()
        {
            EmbedBuilder builder = new EmbedBuilder();

            builder.WithTitle($"Lobby #{this.LobbyNumber}");

            EmbedFieldBuilder powerLevelFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Power Level Range",
                Value = $"{this.LobbyPowerLevel - this.CurrentDelta} - {this.LobbyPowerLevel + this.CurrentDelta}"
            };
            builder.Fields.Add(powerLevelFieldBuilder);

            EmbedFieldBuilder playersFieldBuilder = new EmbedFieldBuilder
            {
                Name = "Players",
                Value = string.Join('\n', this.Players.Select(e => e.DiscordUser.Mention))
            };
            builder.Fields.Add(playersFieldBuilder);

            builder.Footer = new EmbedFooterBuilder();

            DateTime nextInterval = this.LastUpdate.Add(TimeSpan.FromMinutes(5));

            while (nextInterval < DateTime.Now)
            {
                nextInterval = nextInterval.Add(TimeSpan.FromMinutes(5));
            }

            if (!this.IsFull)
                builder.Footer.WithText(
                    $"Next Interval: {nextInterval - DateTime.Now:mm\\:ss} " +
                    $"Queue Time Remaining: {this.LastUpdate.Add(TimeSpan.FromMinutes(15)) - DateTime.Now:mm\\:ss}");

            return builder;
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (this.CurrentDelta * 2 >= 125)
            {
                await this.commandContext.Channel.SendMessageAsync(
                    $"{string.Join(" ", this.players.Select(f => f.DiscordUser.Mention))}\n" +
                    $"Closing the lobby because not enough players have joined the battle. Please try again by using %join.");

                SocketRole rmSetRole = this.commandContext.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({this.LobbyNumber})");
                foreach (SdlPlayer sdlPlayer in this.players)
                {
                    await sdlPlayer.DiscordUser.RemoveRoleAsync(rmSetRole);
                }

                this.Close();

                return;
            }

            this.CurrentDelta += 25;

            SocketRole setRole = this.commandContext.Guild.Roles.FirstOrDefault(e => e.Name == $"In Set ({this.LobbyNumber})");
            SocketRole devRole = this.commandContext.Guild.Roles.First(e => e.Name == "Developer");

            if (setRole == null)
            {
                await devRole.ModifyAsync(e => e.Mentionable = true);
                await this.commandContext.Channel.SendMessageAsync($"{devRole.Mention} Fatal Error! Unable to find In Set role with name \"In Set ({this.LobbyNumber})\".");
                await devRole.ModifyAsync(e => e.Mentionable = false);
                return;
            }

            string message =
                $"{setRole.Mention} {(this.CurrentDelta / 25 - 1) * 5} minutes have passed. The threshold has been increased by 25 to {this.CurrentDelta}.";

            EmbedBuilder builder = this.GetEmbedBuilder();

            await setRole.ModifyAsync(e => e.Mentionable = true);
            await this.commandContext.Channel.SendMessageAsync(message, false, builder.Build());
            await setRole.ModifyAsync(e => e.Mentionable = false);
        }
    }
}
