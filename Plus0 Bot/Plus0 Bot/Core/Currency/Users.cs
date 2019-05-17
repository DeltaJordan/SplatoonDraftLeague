using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Linq;

using Plus0_Bot.Core.Data;
using Plus0_Bot.Resources.Database;

/*This file is functionally the same as the stones file in the tutorial*/
//this file may be deleted, it was jsut part of the tutorial project I followed to learn the ins and outs of Disocrd.net

namespace Plus0_Bot.Currency
{
    public class Users : ModuleBase<SocketCommandContext>
    {
        [Group("user"), Alias("users"), Summary("Group to do stuff with users")]
        public class UsersGroup : ModuleBase<SocketCommandContext>
        {

            //Shows the current stone number of a givenm user if mention or the person that called the command if no one is emntioned
            [Command(""), Alias("me","my"), Summary("Shows the current User's Stones")]
            public async Task Me(IUser User = null)
            {
                //if no one was mentioned, returnt the data of the same person that called the command
                if(User == null)
                    await Context.Channel.SendMessageAsync($"{Context.User}, you have {Data.GetStones(Context.User.Id)}");
                //Otherwise, return the stones count of the mentioned user
                else
                    await Context.Channel.SendMessageAsync($"{User.Username}, you have {Data.GetStones(User.Id)}");


            }
            [Command("give"), Alias("gift"), Summary("Used to give peole User")]
            public async Task Give(IUser User = null,int ammount = 0 )
            {
                //checks that a user was menioned
                if(User == null)
                {
                   
                    await Context.Channel.SendMessageAsync(":x: You didnt mention a user to give the stones to! Please use this syntax: a!users give **<@user>** <ammount>");
                    return;

                }
                //checks that the user is not a bot
                if(User.IsBot)
                {
                    await Context.Channel.SendMessageAsync("That is a bot, you cant give anything to bots");
                    return;
                }
                //checks that a valid ammount was ofered
                if(ammount==0)
                {
                    await Context.Channel.SendMessageAsync($"You must give a valid ammount of currency to  {User.Username}");
                    return;
                }
                //checks administrator privledges
                SocketGuildUser User1 = Context.User as SocketGuildUser;
                if(!User1.GuildPermissions.Administrator)
                {
                    await Context.Channel.SendMessageAsync("You are not an admin, so you cant give currency");
                    return;
                }

                //tells the user what theye got
                await Context.Channel.SendMessageAsync($":tada: {User.Mention} you have recived currency from {Context.User.Username}");

                //calls the command that will update the user data by adding the stone count
                await Data.SaveStones(User.Id, ammount);
            }

            [Command("reset"), Summary("Resets User Data")]
            public async Task Reset (IUser user = null)
            {
                if(user==null)
                {
                    await Context.Channel.SendMessageAsync(":x: A user was not menioned");
                    return;
                }
                if(user.IsBot)
                {
                    await Context.Channel.SendMessageAsync(":x: That is a bot and not a user");
                    return;
                }
                SocketGuildUser User1 = Context.User as SocketGuildUser;
                if(!(User1.GuildPermissions.Administrator))
                {
                    await Context.Channel.SendMessageAsync(":x: You are not an admin");
                    return;
                }

                await Context.Channel.SendMessageAsync($"Hey {user.Mention} your stuff  got reset by {Context.User.Username}, so fight him");
                using (var DBContext = new SqliteDBContext())
                {
                    DBContext.Users.RemoveRange(DBContext.Users.Where(x => x.UserId == user.Id));
                    await DBContext.SaveChangesAsync();
                }
            }
        }
    }
}
