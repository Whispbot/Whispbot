using Newtonsoft.Json;
using OpenAI.Realtime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands
{
    public static partial class AppCommand
    {
        public static readonly Dictionary<SlashCommandArgType, (ApplicationCommandOptionType, bool)> inputTypes = new()
        {
            { SlashCommandArgType.User, (ApplicationCommandOptionType.User, true) },
            { SlashCommandArgType.Role, (ApplicationCommandOptionType.Role, true) },
            { SlashCommandArgType.Channel, (ApplicationCommandOptionType.Channel, true) },
            { SlashCommandArgType.Mentionable, (ApplicationCommandOptionType.Mentionable, true) }
        };

        public static List<ApplicationCommand> GenerateCommands (CommandManager manager)
        {
            List<ApplicationCommand> commands = [];

            foreach (Command cmd in manager.commands)
            {
                if (cmd.SlashCommand is not null && cmd.SlashCommand.Count > 0)
                {
                    List<string> names = cmd.SlashCommand;
                    string? name = names.ElementAtOrDefault(0);
                    string? subCommandGroup = names.ElementAtOrDefault(1);
                    string? subCommand = names.ElementAtOrDefault(2);

                    if (name is null) continue;

                    List<ApplicationCommandOption> options = [];

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

                            options.Add(
                                new ApplicationCommandOption
                                {
                                    name = inputName,
                                    required = !isOptional,
                                    type = typeData.Item1,
                                    autocomplete = typeData.Item2,
                                    description = description,
                                    min_length = arg.min_length,
                                    max_length = arg.max_length,
                                    min_value = arg.min_values,
                                    max_value = arg.max_values
                                }
                            );
                        }
                    }

                    ApplicationCommand command = commands.Find(x => x.name == name) ?? new ApplicationCommand
                    {
                        id = null!,
                        name = name,
                        description = cmd.Description,
                        options = subCommandGroup is not null ? [] : options
                    };

                    if (subCommandGroup is not null)
                    {
                        var group = command.options?.Find(x => x.name == subCommandGroup) ?? new ApplicationCommandOption
                        {
                            name = subCommandGroup,
                            description = cmd.Description,
                            type = subCommand is not null ? ApplicationCommandOptionType.SubCommandGroup : ApplicationCommandOptionType.SubCommand,
                            options = subCommand is not null ? [] : options
                        };

                        if (subCommand is not null)
                        {
                            group.options!.Add(new ApplicationCommandOption
                            {
                                name = subCommand,
                                description = cmd.Description,
                                type = ApplicationCommandOptionType.SubCommand
                            });
                        }

                        command.options!.Remove(group);
                        command.options!.Add(group);
                    }

                    commands.Remove(command);
                    commands.Add(command);
                }
            }

            return commands;
        }

        public static async Task SyncCommands(CommandManager manager, Client client)
        {
            var commands = GenerateCommands(manager);

            HttpClient c = new();

            string? token = Config.isDev ? Environment.GetEnvironmentVariable("DEV_TOKEN") : Environment.GetEnvironmentVariable("CLIENT_TOKEN");

            if (token is null)
            {
                Log.Error("Failed to sync application commands due to missing token.");
                return;
            }

            c.DefaultRequestHeaders.Add("Authorization", $"Bot {token}");

            var result = await c.PutAsync(
                $"https://discord.com/api/v10/applications/{client.readyData?.user.id}/commands",
                new StringContent(JsonConvert.SerializeObject(commands), Encoding.UTF8, "application/json")
            );

            if (result.IsSuccessStatusCode)
            {
                Log.Information("Successfully synced application commands.");
            }
            else
            {
                string errorBody = await result.Content.ReadAsStringAsync();
                object errorData = JsonConvert.DeserializeObject(errorBody)!;
                Logger.WithData(errorData).Error($"Failed to sync application commands with status {(int)result.StatusCode}.");
            }
        }

        [GeneratedRegex(@"<([^:]+):([^?]+)(\??)(.*)>")]
        private static partial Regex CommandOptionRegex();
    }
}
