using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using SquidDraftLeague.Bot.Commands.Preconditions;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Moderation")]
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        [Command("limit"),
         Summary("Very complicated command to modify what commands can be used where and when. " +
                 "This command is used similarly to command prompt commands with arguments. " +
                 "This means that all arguments with spaces need to be wrapped in quotes. " +
                 "This command also assumes that the command is to be limited to deny according " +
                 "to the arguments unless specified otherwise."),
         RequireRole("Moderator")]
        public async Task Limit(params string[] args)
        {
            List<KeyValuePair<string, string[]>> argumentPairs = new List<KeyValuePair<string, string[]>>();

            for (int i = 0; i < args.Length; i++)
            {


                if (args[i].StartsWith("-"))
                {

                }
            }
        }
    }
}
