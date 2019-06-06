using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

namespace SquidDraftLeague.Bot.Commands.Criterions
{
    public class HasRoleCriterion : ICriterion<SocketMessage>
    {
        private readonly ulong id;

        public HasRoleCriterion(IRole role)
        {
            this.id = role.Id;
        }

        public HasRoleCriterion(ulong id)
        {
            this.id = id;
        }

        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
        {
            if (parameter.Author is IGuildUser user)
            {
                return Task.FromResult(user.RoleIds.Contains(this.id));
            }

            return Task.FromResult(false);
        }
    }
}
