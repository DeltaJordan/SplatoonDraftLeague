using System.Linq;
using System.Threading.Tasks;

namespace SquidDraftLeague.Bot.Commands.Criterions
{
    public class HasRoleCriterion
    {
        // TODO D#+ doesn't use this?

        /*private readonly ulong id;

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
        }*/
    }
}
