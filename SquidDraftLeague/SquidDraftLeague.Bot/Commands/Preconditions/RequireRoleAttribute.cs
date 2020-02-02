using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class RequireRoleAttribute : CheckBaseAttribute
    {
        // Create a field to store the specified name
        private readonly string name;
        private readonly ulong id;

        // Create a constructor so the name can be specified
        public RequireRoleAttribute(string name) => this.name = name;

        // Create a constructor so the id can be specified
        public RequireRoleAttribute(ulong id) => this.id = id;

        public override Task<bool> CanExecute(CommandContext ctx, bool help)
        {
            if (Globals.SuperUsers != null && Globals.SuperUsers.Contains(ctx.User.Id))
            {
                return Task.FromResult(true);
            }

            if (ctx.Guild.Id == 593978296027316224)
            {
                return Task.FromResult(true);
            }

            if (ctx.Guild == null || ctx.Guild.Id != 570743985530863649)
            {
                return Task.FromResult(false);
            }

            // Check if this user is a Guild User, which is the only context where roles exist
            if (ctx.User is DiscordMember gUser)
            {
                // If this command was executed by a user with the appropriate role, return a success
                return Task.FromResult(gUser.Roles.Any(r => r.Name == this.name || r.Id == this.id));
            }

            return Task.FromResult(false);
        }
    }
}
