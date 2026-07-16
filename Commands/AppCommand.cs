using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using OpenAI.Realtime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Whispbot.Commands
{
    public static partial class AppCommand
    {
        public static readonly Dictionary<CommandArgType, (ApplicationCommandOptionType, bool)> inputTypes = new()
        {
            { CommandArgType.User, (ApplicationCommandOptionType.User, true) },
            { CommandArgType.Role, (ApplicationCommandOptionType.Role, true) },
            { CommandArgType.Channel, (ApplicationCommandOptionType.Channel, true) },
            { CommandArgType.Mentionable, (ApplicationCommandOptionType.Mentionable, true) }
        };

        public static List<SlashCommandProperties> GenerateCommands ()
        {
            List<SlashCommandBuilder> commands = [];

            foreach (Command cmd in CommandManager.commands)
            {
                if (cmd.SlashCommand is not null && cmd.SlashCommand.Count > 0)
                {
                    List<string> names = cmd.SlashCommand;
                    string? name = names.ElementAtOrDefault(0);
                    string? subCommandGroup = names.ElementAtOrDefault(1);
                    string? subCommand = names.ElementAtOrDefault(2);

                    if (name is null) continue;

                    List<SlashCommandOptionBuilder> options = [];

                    if (cmd.Arguments is not null)
                    {
                        foreach (var arg in cmd.Arguments)
                        {
                            var inputName = arg.name;
                            var inputType = arg.type;
                            var description = arg.description;
                            var isOptional = arg.optional;

                            var typeData = inputTypes.TryGetValue(inputType, out var temp) 
                                ? temp 
                                : (ApplicationCommandOptionType.String, true);

                            var opt = new SlashCommandOptionBuilder()
                                .WithName(inputName)
                                .WithDescription(description)
                                .WithRequired(!isOptional)
                                .WithType(typeData.Item1)
                                .WithAutocomplete(typeData.Item2);

                            if (arg.min_length is not null) opt.WithMinLength(arg.min_length.Value);
                            if (arg.max_length is not null) opt.WithMaxLength(arg.max_length.Value);
                            if (arg.min_values is not null) opt.WithMinValue(arg.min_values.Value);
                            if (arg.max_values is not null) opt.WithMaxValue(arg.max_values.Value);

                            options.Add(opt);
                        }
                    }

                    var command = commands.Find(x => x.Name == name) ?? new SlashCommandBuilder()
                        .WithName(name)
                        .WithDescription(cmd.Description);

                    if (subCommandGroup is not null)
                    {
                        var group = command.Options?.Find(x => x.Name == subCommandGroup) ?? 
                            new SlashCommandOptionBuilder()
                                .WithName(subCommandGroup)
                                .WithDescription(cmd.Description)
                                .WithType(subCommand is not null ? ApplicationCommandOptionType.SubCommandGroup : ApplicationCommandOptionType.SubCommand);

                        if (subCommand is not null)
                        {
                            var subCommandOption = new SlashCommandOptionBuilder()
                                .WithName(subCommand)
                                .WithDescription(cmd.Description)
                                .WithType(ApplicationCommandOptionType.SubCommand);

                            options.ForEach(x => subCommandOption.AddOption(x));

                            group.AddOption(subCommandOption);
                        }
                        else options.ForEach(x => group.AddOption(x));

                        command.Options!.Remove(group);
                        command.Options!.Add(group);
                    }
                    else options.ForEach(x => command.AddOption(x));


                    commands.Remove(command);
                    commands.Add(command);
                }
            }

            return [.. commands.Select(c => c.Build())];
        }

        public static async Task SyncCommands(DiscordShardedClient client)
        {
            var commands = GenerateCommands();

            await client.BulkOverwriteGlobalApplicationCommandsAsync([.. commands]);

            Log.Information("Synced application commands!");
        }
    }
}
