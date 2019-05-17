using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;


using Discord;
using Discord.Commands;

namespace Plus0_Bot.Core.Commands
{

    public class DraftCommand : ModuleBase<SocketCommandContext>
    {


        public static List<IUser> DraftMembers = new List<IUser>();
       


        //[Group("Draft"), Summary("Commands to join, view, enter, or edit drafts")]

        [Command("Join"), Summary("Starts a draft if one has not been started, or joins one")]
        public async Task Join()
        {
            EmbedBuilder DraftEmbed = new EmbedBuilder();
            String drafties = "";

            if (DraftMembers.Contains(Context.User))
            {
                await Context.Channel.SendMessageAsync(":x: Cannot join draft\n You are already in the draft");

            }
            if (!(DraftMembers.Count < 8))
            {
                await Context.Channel.SendMessageAsync(":x: Cannot join draft\n The Draft you tried to join is currently full");
               

            }
            else
            {
                await Context.Channel.SendMessageAsync("Joining Draft");
                DraftMembers.Add(Context.User);

                drafties = "";

                foreach (var user in DraftMembers)
                {
                    drafties += "@<" + user.Username + ">\n";
                }
                DraftEmbed.WithDescription("The current " + DraftMembers.Count +
                    " members in the draft are: \n" + drafties);
                await Context.Channel.SendMessageAsync("", false, DraftEmbed.Build());

            }
        }

        [Command("Leave"), Summary("Leaves the Curent draft")]
        public async Task Leave()
        {
            if (!DraftMembers.Contains(Context.User))
            {
                await Context.Channel.SendMessageAsync(":x: You are not in a Draft");

            }
            else
            {
                DraftMembers.Remove(Context.User);
                await Context.Channel.SendMessageAsync("You have been removed from the draft");

            }
        }
    }
}
