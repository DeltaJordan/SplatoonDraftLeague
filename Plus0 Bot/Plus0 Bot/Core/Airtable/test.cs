using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirtableApiClient;

using Discord;
using Discord.Commands;

namespace Plus0_Bot.Core.Airtable
{

    public class test:ModuleBase<SocketCommandContext>
    {

         string baseId = "appHvHRfmPn96vjoL";
         string appKey = "keyCBOb1hwDrUgXdl";

        [Command("ABtest"), Summary("testing Airtable api")]
        public async Task ABtest()
        {
            await Context.Channel.SendMessageAsync(Data.GetUsername(3).ToString());
        }

           
    }
}
