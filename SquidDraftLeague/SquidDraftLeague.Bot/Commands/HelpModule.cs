using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SquidDraftLeague.Bot.Commands.Attributes;

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

        [Command("help")]
        public async Task HelpAsync(string command)
        {
            SearchResult result = this.service.Search(this.Context, command);

            if (!result.IsSuccess)
            {
                await this.ReplyAsync($"Sorry, I couldn't find a command like **{command}**.");
                return;
            }

            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Description = $"Here are some commands like **{command}**"
            };

            foreach (CommandMatch match in result.Commands)
            {
                CommandInfo cmd = match.Command;

                builder.AddField(x =>
                {
                    x.Name = string.Join(", ", cmd.Aliases);

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
                });
            }

            await this.ReplyAsync("", false, builder.Build());
        }
    }
}
