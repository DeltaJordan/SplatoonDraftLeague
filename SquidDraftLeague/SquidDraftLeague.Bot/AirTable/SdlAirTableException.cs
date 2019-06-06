using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace SquidDraftLeague.Bot.AirTable
{
    public class SdlAirTableException : Exception
    {
        public enum AirtableErrorType
        {
            NotFound,
            UnexpectedDuplicate,
            CommunicationError,
            Generic
        }

        public AirtableErrorType ErrorType { get; }

        public SdlAirTableException(string message, AirtableErrorType errorType) 
            : base(message)
        {
            this.ErrorType = errorType;
        }

        public async Task OutputToDiscordUser(SocketCommandContext context)
        {
            SocketRole devRole = context.Guild.Roles.First(e => e.Name == "Developer");

            IUser player = context.User;

            switch (this.ErrorType)
            {
                case AirtableErrorType.NotFound:
                    await context.Channel.SendMessageAsync("You do not appear to be registered.");
                    return;
                case AirtableErrorType.UnexpectedDuplicate:
                    await devRole.ModifyAsync(e => e.Mentionable = true);
                    await context.Channel.SendMessageAsync($"{devRole.Mention}: {player.Mention} ({player.Id}) has two or more records in the drafting table!");
                    await devRole.ModifyAsync(e => e.Mentionable = false);
                    return;
                case AirtableErrorType.CommunicationError:
                    await devRole.ModifyAsync(e => e.Mentionable = true);
                    await context.Channel.SendMessageAsync($"{devRole.Mention}: {player.Mention} ({player.Id}) had a communication error during retrieving their records." +
                                          $"\n{this.Message}");
                    await devRole.ModifyAsync(e => e.Mentionable = false);
                    return;
                case AirtableErrorType.Generic:
                    await devRole.ModifyAsync(e => e.Mentionable = true);
                    await context.Channel.SendMessageAsync($"{devRole.Mention}: {player.Mention} ({player.Id}) a generic error occured during their record retrieval." +
                                          $"\n{this.Message}");
                    await devRole.ModifyAsync(e => e.Mentionable = false);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
