using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Commands.ERLC;
using Whispbot.Commands.General;
using Whispbot.Commands.Roblox_Moderation;
using Whispbot.Commands.Shifts;
using Whispbot.Commands.Staff;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using YellowMacaroni.Discord.Sharding;

namespace Whispbot.Commands
{
    public class CommandManager
    {
        public readonly List<Command> commands = [];
        public readonly List<Command> staffCommands = [];

        public readonly Dictionary<string, RatelimitData> ratelimits = [];

        public CommandManager()
        {

            #region Commands

            RegisterCommand(new Ping());
            RegisterCommand(new About());
            RegisterCommand(new Support());
            RegisterCommand(new Prefix());
            RegisterCommand(new Connections());

            RegisterCommand(new Clockin());
            RegisterCommand(new Clockout());
            RegisterCommand(new ShiftManage());
            RegisterCommand(new ShiftAdmin());
            RegisterCommand(new ShiftLeaderboard());
            RegisterCommand(new ShiftActivity());
            RegisterCommand(new ShiftActive());

            RegisterCommand(new LogModeration());
            RegisterCommand(new RobloxCase());
            RegisterCommand(new RobloxReason());
            RegisterCommand(new RobloxType());
            RegisterCommand(new RobloxVoid());
            RegisterCommand(new Roblox_Moderation.BanRequest());

            RegisterCommand(new ERLC_ServerInfo());
            RegisterCommand(new ERLC_Players());
            RegisterCommand(new ERLC_Queue());
            RegisterCommand(new ERLC_Vehicles());
            RegisterCommand(new ERLC_JoinLogs());
            RegisterCommand(new ERLC_JoinServer());
            RegisterCommand(new ERLC_KillLogs());
            RegisterCommand(new ERLC_CommandLogs());
            RegisterCommand(new ERLC_ModCalls());
            RegisterCommand(new ERLC_Banned());
            RegisterCommand(new ERLC_VSM());

            RegisterStaffCommand(new Test());
            RegisterStaffCommand(new SQL());
            RegisterStaffCommand(new UpdateLanguages());
            RegisterStaffCommand(new AIRequest());
            RegisterStaffCommand(new ResolveError());

            #endregion

            Log.Debug($"Loaded {commands.Count} commands");
        }

        public void RegisterCommand(Command command)
        {
            if (commands.Any(c => c.Name == command.Name)) return;
            commands.Add(command);
        }

        public void RegisterStaffCommand(Command command)
        {
            if (staffCommands.Any(c => c.Name == command.Name)) return;
            staffCommands.Add(command);
        }

        private int? _maxLength = null;
        public int MaxLength
        {
            get
            {
                _maxLength ??= commands.Max(c => c.Aliases.Max(a => a.Split(" ").Length));
                return _maxLength ?? 0;
            }
        }

        public async Task HandleMessage(Client client, Message message)
        {
            if (message.webhook_id is not null)
            {
                Config.erlcCommands?.HandleMessage(client, message);
                return;
            }

            GuildConfig? guildConfig = message.channel?.guild_id is not null ? await WhispCache.GuildConfig.Get(message.channel.guild_id) : null;

            string prefix = guildConfig?.prefix ?? Config.prefix;
            string mention = $"<@{client.readyData?.user.id}>";

            string staffPrefix = Config.staffPrefix;

            if (message.content.StartsWith(mention)) prefix = mention;

            if (message.content.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                List<string> args = [.. message.content[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
                string content = args.Join(" ");

                Command? command = null;
                for (int len = MaxLength; len > 0; len--)
                {
                    Command? activeCommand = commands.Find(c =>
                    {
                        foreach (string alias in c.Aliases)
                        {
                            int length = alias.Split(" ").Length;
                            if (length == len && (content.StartsWith($"{alias} ", StringComparison.CurrentCultureIgnoreCase) || content.Equals(alias, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                args.RemoveRange(0, length);
                                return true;
                            }
                        }
                        return false;
                    });
                    if (activeCommand is not null)
                    {
                        command = activeCommand;
                        break;
                    }
                }

                if (command is null) return;

                MatchCollection matches = Regex.Matches(args.Join(" "), @"--(\w+)");
                List<string> flags = [.. matches.Select(m => m.Groups[1].Value)];
                args = [.. args.Where(a => !flags.Contains(a))];

                var ctx = new CommandContext(client, message, args, flags);

                if (ctx.GuildConfig is null)
                {
                    await ctx.Reply("{emoji.warning} {string.errors.dbfailed}");
                    return;
                }

                if (ctx.GuildConfig.version != Config.EnvId)
                {
                    return;
                }
                

                if (ctx.UserConfig?.ack_required ?? false)
                {
                    await ctx.Reply(Actions.GenerateAcknowledgeMessage(long.Parse(ctx.UserId ?? "0")));

                    return;
                }

                if (command.Ratelimits.Count > 0)
                {
                    foreach (var rl in command!.Ratelimits)
                    {
                        string rlk = rl.type switch
                        {
                            RateLimitType.Global => "global",
                            RateLimitType.Guild => message.channel?.guild_id ?? "global",
                            RateLimitType.User => message.author.id,
                            _ => "global"
                        };

                        string key = $"{command.Name}:{rlk}";

                        RatelimitData? data = ratelimits.GetValueOrDefault(key);
                        if (data is null)
                        {
                            ratelimits[key] = new RatelimitData()
                            {
                                Remaining = rl.amount - 1,
                                Reset = DateTimeOffset.UtcNow + rl.per
                            };
                        }
                        else
                        {
                            if (data.Remaining == 0 && data.Reset > DateTimeOffset.UtcNow)
                            {
                                await ctx.Reply("{string.errors.ratelimited}".Process(ctx.Language, new Dictionary<string, string>() { { "reset", Time.ConvertMillisecondsToRelativeString(data.Reset.ToUnixTimeMilliseconds(), false, ", ", false, 1000) } }));

                                return;
                            }
                            else
                            {
                                if (data.Reset <= DateTimeOffset.UtcNow)
                                {
                                    data.Remaining = rl.amount - 1;
                                    data.Reset = DateTimeOffset.UtcNow + rl.per;
                                }
                                else
                                {
                                    data.Remaining--;
                                }
                            }
                        }
                    }
                }

                try
                {
                    await command.ExecuteAsync(ctx);
                }
                catch (Exception ex)
                {
                    var id = SentrySdk.CaptureException(ex);

                    var e_result = await ctx.EditResponse(new MessageBuilder
                    {
                        components = [
                            new ContainerBuilder
                            {
                                components = [
                                    new TextDisplayBuilder("## {string.title.error}"),
                                    new TextDisplayBuilder("{string.content.error}".Process(ctx.Language, new() { { "url", "<https://whisp.bot/support>" } })),
                                    new TextDisplayBuilder($"{{string.content.error.id}}:\n```\n{id}\n```"),
                                    new SectionBuilder
                                    {
                                        components = [
                                            new TextDisplayBuilder("{string.content.error.feedback}")
                                        ],
                                        accessory = new ButtonBuilder
                                        {
                                            label = "{string.content.error.feedback_button}",
                                            custom_id = $"error_feedback {ctx.UserId} {id}",
                                            style = ButtonStyle.Secondary
                                        }
                                    }
                                ],
                                accent = new(200, 69, 69)
                            }
                        ],
                        flags = MessageFlags.IsComponentsV2
                    });

                    if (e_result.Item2 is not null) Log.Error(e_result.Item2.ToString());
                }
            }
            else if 
            (
                message.content.StartsWith(staffPrefix, StringComparison.CurrentCultureIgnoreCase)
                //                          |   Support Server  |             ->          |     Member    |               ->            |  Has Staff Role?  |
                && (DiscordCache.Guilds.Get("1096509172784300174").WaitFor()?.members.Get(message.author.id).WaitFor()?.roles?.Contains("1256333207599841435") ?? false)
            )
            {
                List<string> args = [.. message.content[staffPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
                string content = args.Join(" ");

                Command? command = null;
                for (int len = MaxLength; len > 0; len--)
                {
                    Command? activeCommand = staffCommands.Find(c =>
                    {
                        foreach (string alias in c.Aliases)
                        {
                            int length = alias.Split(" ").Length;
                            if (length == len && (content.StartsWith($"{alias} ", StringComparison.CurrentCultureIgnoreCase) || content.Equals(alias, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                args.RemoveRange(0, length);
                                return true;
                            }
                        }
                        return false;
                    });
                    if (activeCommand is not null)
                    {
                        command = activeCommand;
                        break;
                    }
                }

                MatchCollection matches = Regex.Matches(args.Join(" "), @"--(\w+)");
                List<string> flags = [.. matches.Select(m => m.Groups[1].Value)];
                args = [.. args.Where(a => !flags.Contains(a))];

                command?.ExecuteAsync(new CommandContext(client, message, args, flags));
            }
        }

        public void Attach(Client client)
        {
            client.MessageCreate += async (c, message) =>
            {
                if (c is not Client client) return;
                await HandleMessage(client, message);
            };
        }

        public void Attach(ShardingManager manager)
        {
            foreach (Shard shard in manager.shards)
            {
                Attach(shard.client);
            }
        }






        public class RatelimitData
        {
            public int Remaining;
            public DateTimeOffset Reset;
        }
    }
}
