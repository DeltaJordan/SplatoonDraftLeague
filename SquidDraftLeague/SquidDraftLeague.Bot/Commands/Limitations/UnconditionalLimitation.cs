using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public class UnconditionalLimitation : ILimitation
    {
        public bool Inverse { get; set; }
        public async Task<bool> CheckLimitationAsync(SocketCommandContext context) => this.Inverse;
    }
}
