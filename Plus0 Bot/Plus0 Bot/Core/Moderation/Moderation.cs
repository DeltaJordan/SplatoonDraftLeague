using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using System.Reflection;

using Plus0_Bot.Resources.Settings;
using Newtonsoft.Json;
using Plus0_Bot.Resources.Datatypes;

namespace Plus0_Bot.Core.Moderation
{
    public class Moderation : ModuleBase<SocketCommandContext>
    {
        [Command("reload"), Summary("Reloads Json file while bot is running")]
        public async Task Reload()
        {
            //checks
            if(Context.User.Id != ESettings.Owner)
            {
                await Context.Channel.SendMessageAsync(":x: You are not the Owner");
                return;
            }
            string SettingsLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location.Replace(@"bin\Debug\netcoreapp2.1", @"Data\Settings.json"));

            if (!File.Exists(SettingsLocation))
            {
                await Context.Channel.SendMessageAsync(":x: The file is not found in the given location. The expected location can be found in the log");
                Console.WriteLine(SettingsLocation);
                return;
            }
            //execution
            string JSON = "";
            using (var Stream = new FileStream(SettingsLocation, FileMode.Open, FileAccess.Read))
            using (var ReadSettings = new StreamReader(Stream))
            {
                JSON = ReadSettings.ReadToEnd();
            }

            Setting Settings = JsonConvert.DeserializeObject<Setting>(JSON);

            //Save
            ESettings.Banned = Settings.banned;
            ESettings.Log = Settings.log;
            ESettings.Owner = Settings.owner;
            ESettings.Token = Settings.token;
            ESettings.Version = Settings.version;

            await Context.Channel.SendMessageAsync(":white_check_mark: All Settings updated succesfully");
        }
    }
}
