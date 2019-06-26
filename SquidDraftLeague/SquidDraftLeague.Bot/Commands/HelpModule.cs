using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SquidDraftLeague.Bot.Commands.Attributes;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Bot.Commands
{
    [Name("Help")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {

        private readonly CommandService service;

        public HelpModule(CommandService service)
        {
            this.service = service;
        }

        [Command("help")]
        public async Task HelpAsync()
        {
            try
            {
                EmbedBuilder builder = new EmbedBuilder
                {
                    Color = new Color(114, 137, 218),
                    Title = "__Available Commands__",
                    Description = "(Parameter Name) = Required (Do not put the parentheses)\n" +
                                  "[Parameter Name] = Optional (Do not put the brackets)\n" +
                                  "\"Parameter Name\" = Needs to be wrapped in quotes to detect spaces."
                };

                foreach (ModuleInfo module in this.service.Modules)
                {
                    string description = null;
                    foreach (CommandInfo cmd in module.Commands)
                    {
                        PreconditionResult result = await cmd.CheckPreconditionsAsync(this.Context);
                        if (result.IsSuccess)
                        {
                            description += Globals.BotSettings.Prefix + $"{cmd.Aliases.First()}";

                            foreach (ParameterInfo parameterInfo in cmd.Parameters)
                            {
                                description += " ";

                                string parameterName = parameterInfo.IsOptional
                                    ? $"[{parameterInfo.Name}]"
                                    : $"({parameterInfo.Name})";

                                parameterName = parameterInfo.IsRemainder ? parameterName : $"\"{parameterName}\"";

                                description += parameterName;
                            }

                            description += "\n";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        builder.AddField(x =>
                        {
                            x.Name = module.Name + " Commands";
                            x.Value = description;
                            x.IsInline = false;
                        });
                    }
                }

                await this.ReplyAsync("", false, builder.Build());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Command("help")]
        public async Task HelpAsync(string command)
        {
            SearchResult result = this.service.Search(this.Context, command);

            if (!result.IsSuccess)
            {
                await this.ReplyAsync($"Sorry, I couldn't find a command like **{command}**.");
                return;
            }

            EmbedBuilder builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = $"Here are some commands like **{command}**"
            };

            bool limit = false;

            foreach (CommandMatch match in result.Commands)
            {
                CommandInfo cmd = match.Command;

                builder.AddField(x =>
                {
                    x.Name = string.Join(", ", cmd.Aliases);

                    if (cmd.Name != "limit")
                    {
                        string parameters = string.Join("\n", 
                            cmd.Parameters.Select(p => $"{p.Name} - {p.Summary}"));

                        if (string.IsNullOrWhiteSpace(parameters))
                        {
                            parameters = "None.";
                        }

                        ExampleCommandAttribute exampleCommand =
                            (ExampleCommandAttribute) cmd.Attributes.FirstOrDefault(e => e is ExampleCommandAttribute);

                        string exampleUsage = exampleCommand != null ? 
                            $"\n__Example Usage:__\n{exampleCommand.Example}" : 
                            "";

                        x.Value = $"*{cmd.Summary}*\n" +
                                  $"__Parameters:__\n{parameters}" +
                                  exampleUsage;
                        x.IsInline = false;
                    }
                    else
                    {
                            limit = true;

                            x.Name = string.Join(", ", cmd.Aliases);
                            x.Value = $"*{cmd.Summary}*";
                            x.IsInline = false;
                    }
                });
            }


            string arguments = "**__Arguments (In order of hierarchy):__**\n" +
                               "**--clear** - Removes all limitations from a command/group. Ignores all other arguments other than -g and -c.\n" +
                               "**--now** - Limits the command immediately and at all times. Ignores all other arguments other than -g and -c.\n" +
                               "**--all** - Selects every command in the bot. Ignores -g and -c.\n" +
                               "**--deny** - Switches this limit instance to deny according to specified terms instead of allow.\n" +
                               "**-c [Command Name1] [Command Name...], --command [Command Name1] [Command Name...]** - Applies the limit only to the specified command(s).\n" +
                               "**-g [Group Name1] [Group Name...], --group [Group Name1] [Group Name...]** - Applies the limit only to the specified group(s). " +
                               $"Must select one of the following groups: {string.Join(", ", this.service.Modules.Select(e => e.Name))}\n" +
                               $"**-t [timeStart1] [timeEnd1] [timeStart..] [timeEnd..]** - A list of start and end times formatted HH:mm to limit a command between. " +
                               $"Times MUST be in 24 hour formatted UTC for the simple fact that this is the easiest timezone to work with.\n" +
                               $"**-ch [Channel ID], --channel [Channel ID]** - Limits the usage in a specified channel. " +
                               $"If no channel is specified, the context channel is assumed to be selected.\n" +
                               $"**-r [Role Name], --role [Role Name]** - Limit the usage to the specified role name.";

            await this.ReplyAsync("", false, builder.Build());

            if (limit)
                await this.ReplyAsync(arguments);
        }
    }
}
