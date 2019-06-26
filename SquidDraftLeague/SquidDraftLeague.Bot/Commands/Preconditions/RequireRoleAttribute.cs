﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands.Preconditions
{
    public class RequireRoleAttribute : PreconditionAttribute
    {
        // Create a field to store the specified name
        private readonly string name;
        private readonly ulong id;

        // Create a constructor so the name can be specified
        public RequireRoleAttribute(string name) => this.name = name;

        // Create a constructor so the id can be specified
        public RequireRoleAttribute(ulong id) => this.id = id;

        // Override the CheckPermissions method
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (Globals.SuperUsers != null && Globals.SuperUsers.Contains(context.User.Id))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            if (context.Guild == null || context.Guild.Id != 570743985530863649)
            {
                return Task.FromResult(PreconditionResult.FromError("This bot can only be used in approved guilds."));
            }

            // Check if this user is a Guild User, which is the only context where roles exist
            if (context.User is SocketGuildUser gUser)
            {
                // If this command was executed by a user with the appropriate role, return a success
                return Task.FromResult(gUser.Roles.Any(r => r.Name == this.name || r.Id == this.id) ? 
                    PreconditionResult.FromSuccess() : 
                    PreconditionResult.FromError($"{gUser.Username} must have the role {(this.name == null ? this.name : this.id.ToString())} to run this command."));
            }

            return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
        }
    }
}
