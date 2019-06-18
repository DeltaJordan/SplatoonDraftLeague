using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace SquidDraftLeague.Bot.Commands.Limitations
{
    public class RoleLimitation : ILimitation
    {
        public bool Inverse { get; set; }

        public string RoleName { get; }

        public RoleLimitation(string roleName)
        {
            this.RoleName = roleName;
        }

        public async Task<bool> CheckLimitationAsync(SocketCommandContext context)
        {
            if (!(context.User is SocketGuildUser user))
            {
                return false;
            }

            return !this.Inverse == user.Roles.Any(e => string.Equals(this.RoleName, e.Name, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
