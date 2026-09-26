using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Extensions;
using Whispbot.Tools;
using Whispbot.Tools.Disc;

namespace Whispbot.Interactions
{
    public static class Commands
    {
        public static async Task Handle(DiscordShardedClient client, SocketInteraction rawInteraction)
        {
            if (rawInteraction is not SocketSlashCommand interaction) return;

            var options = GetOptions(interaction, out var commandNames);

            var command = Autocomplete.GetCommand(commandNames);
            if (command is null)
            {
                await interaction.RespondAsync(
                    $"Could not find the command '/{commandNames.Join(" ")}'...",
                    ephemeral: true
                );
                return;
            }

            var arguments = await GetArguments(interaction, command, options ?? []);
            if (arguments is null) return;

            var ctx = new CommandContext(client, interaction, arguments);

            await command.ExecuteAsync(ctx);
        }

        public static readonly Dictionary<CommandArgType, Func<SocketSlashCommand, string, dynamic, Task<(CommandArgument?, string?)>>> argParsers = new () 
        {
            { CommandArgType.User, UserArg },
            { CommandArgType.RobloxUser, RobloxUserArg },
            { CommandArgType.Duration, DurationArg }
        };

        public static async Task<CommandArguments?> GetArguments(SocketSlashCommand interaction, Command command, List<SocketSlashCommandDataOption> options)
        {
            CommandArguments args = new();

            foreach (var option in command.Arguments ?? [])
            {
                var func = argParsers.GetValueOrDefault(option.type, Default);
                if (func is null) continue;

                var thisOpt = options.FirstOrDefault(o => o.Name == option.name);
                if (thisOpt is null) continue;

                var result = await func(interaction, option.name, thisOpt.Value);
                var (arg, error) = ((CommandArgument?, string?))result;

                if (error is not null)
                {
                    await interaction.RespondAsync($"err: {error}", ephemeral: true);
                    return null;
                }
                else if (arg is not null)
                {
                    args.args.Add(arg.name, arg);
                }
            }

            return args;
        }

        public static async Task<(CommandArgument?, string?)> Default(SocketSlashCommand interaction, string name, dynamic value)
        {
            return (new(name, value), null);
        }

        public static List<SocketSlashCommandDataOption>? GetOptions(SocketSlashCommand interaction, out List<string> names)
        {
            var firstOption = interaction.Data.Options.FirstOrDefault();
            if (firstOption is null) { names = [interaction.Data.Name]; return []; }

            if (firstOption.Type == ApplicationCommandOptionType.SubCommandGroup || firstOption.Type == ApplicationCommandOptionType.SubCommand)
            {
                names = [interaction.Data.Name, firstOption.Name];
                var data = GetOptions(firstOption, names, out names);
                if (data is null) return [..firstOption.Options];
                else return data;
            }
            else
            {
                names = [interaction.Data.Name, firstOption.Name];
                return GetOptions(firstOption, names, out names);
            }
        }

        public static List<SocketSlashCommandDataOption>? GetOptions(SocketSlashCommandDataOption option, List<string> names, out List<string> outNames)
        {
            var opt = option.Options?.FirstOrDefault();
            if (opt is null) { outNames = names; return null; }

            if (opt.Type == ApplicationCommandOptionType.SubCommandGroup || opt.Type == ApplicationCommandOptionType.SubCommand)
            {
                names.Add(opt.Name ?? "what the sigma");
                var data = GetOptions(opt, names, out outNames);
                if (data is null) return [..opt.Options];
                else return data;
            }
            else
            {
                outNames = names;
                return null;
            }
        }

        public static async Task<(CommandArgument?, string?)> UserArg(SocketSlashCommand interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid user."); 

            IUser? user = await Users.GetUserByString(str, 3, interaction.GuildId);
            if (user is null) return (null, $"Could not find that user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> RobloxUserArg(SocketSlashCommand interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid Roblox user.");

            Roblox.RobloxUser? user = await Roblox.GetUser(str);
            if (user is null) return (null, $"Could not find that Roblox user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> DurationArg(SocketSlashCommand interaction, string name, dynamic value)
        {
            if (value is not string str) return (null, "Invalid duration.");

            double ms = Time.ConvertStringToMilliseconds(str);

            TimeSpan duration = TimeSpan.FromMilliseconds(ms);

            return (new CommandArgument(name, duration), null);
        }
    }
}
