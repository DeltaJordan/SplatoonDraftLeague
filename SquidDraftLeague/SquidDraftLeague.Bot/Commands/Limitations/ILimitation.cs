using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public interface ILimitation
    {
        bool Inverse { get; set; }

        Task<bool> CheckLimitationAsync(SocketCommandContext context);
    }
}
