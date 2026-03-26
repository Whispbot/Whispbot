using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions
{
    public static class Commands
    {
        public static async Task Handle(Client client, Interaction interaction)
        {
            var options = GetOptions(interaction, out var commandNames);

            var command = Autocomplete.GetCommand(commandNames);
            if (command is null)
            {
                await interaction.Respond(
                    new MessageBuilder { 
                        content = $"Could not find the command '/{commandNames.Join(" ")}'...",
                        flags = MessageFlags.Ephemeral
                    }
                );
                return;
            }

            var arguments = await GetArguments(interaction, command, options ?? []);
            if (arguments is null) return;

            var ctx = new CommandContext(client, interaction, arguments);

            await command.ExecuteAsync(ctx);
        }

        public static readonly Dictionary<CommandArgType, Func<Interaction, string, dynamic, Task<(CommandArgument?, string?)>>> argParsers = new () 
        {
            { CommandArgType.User, UserArg },
            { CommandArgType.RobloxUser, RobloxUserArg },
            { CommandArgType.Duration, DurationArg }
        };

        public static async Task<CommandArguments?> GetArguments(Interaction interaction, Command command, List<ApplicationCommandInteractionDataOption> options)
        {
            CommandArguments args = new();

            foreach (var option in command.Arguments ?? [])
            {
                var func = argParsers.GetValueOrDefault(option.type, Default);
                if (func is null) continue;

                var thisOpt = options.FirstOrDefault(o => o.name == option.name);
                if (thisOpt is null) continue;

                var result = await func(interaction, option.name, thisOpt.value); // why no work
                var (arg, error) = ((CommandArgument?, string?))result;

                if (error is not null)
                {
                    await interaction.Respond(new MessageBuilder { content = $"err: {error}", flags = MessageFlags.Ephemeral });
                    return null;
                }
                else if (arg is not null)
                {
                    args.args.Add(arg.name, arg);
                }
            }

            return args;
        }

        public static async Task<(CommandArgument?, string?)> Default(Interaction interaction, string name, dynamic value)
        {
            return (new(name, value), null);
        }

        public static List<ApplicationCommandInteractionDataOption>? GetOptions(Interaction interaction, out List<string> names)
        {
            var firstOption = interaction.data?.options?.FirstOrDefault();
            if (firstOption is null) { names = [interaction.data?.name!]; return []; }

            if (firstOption.type == ApplicationCommandOptionType.SubCommandGroup || firstOption.type == ApplicationCommandOptionType.SubCommand)
            {
                names = [interaction.data?.name!, firstOption.name!];
                var data = GetOptions(firstOption, names, out names);
                if (data is null) return firstOption.options;
                else return data;
            }
            else
            {
                names = [interaction.data?.name!, firstOption.name!];
                return GetOptions(firstOption, names, out names);
            }
        }

        public static List<ApplicationCommandInteractionDataOption>? GetOptions(ApplicationCommandInteractionDataOption option, List<string> names, out List<string> outNames)
        {
            var opt = option.options?.FirstOrDefault();
            if (opt is null) { outNames = names; return null; }

            if (opt.type == ApplicationCommandOptionType.SubCommandGroup || opt.type == ApplicationCommandOptionType.SubCommand)
            {
                names.Add(opt.name ?? "what the sigma");
                var data = GetOptions(opt, names, out outNames);
                if (data is null) return opt.options;
                else return data;
            }
            else
            {
                outNames = names;
                return null;
            }
        }

        public static async Task<(CommandArgument?, string?)> UserArg(Interaction interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid user."); 

            User? user = await Users.GetUserByString(str, 3, interaction.guild_id);
            if (user is null) return (null, $"Could not find that user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> RobloxUserArg(Interaction interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid Roblox user.");

            Roblox.RobloxUser? user = await Roblox.GetUser(str);
            if (user is null) return (null, $"Could not find that Roblox user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> DurationArg(Interaction interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid duration.");

            double ms = Time.ConvertStringToMilliseconds(str);

            TimeSpan duration = TimeSpan.FromMilliseconds(ms);

            return (new CommandArgument(name, duration), null);
        }
    }
}
