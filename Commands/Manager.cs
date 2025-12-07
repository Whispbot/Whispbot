using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Commands.ERLCCommands;
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
                // Relay ER:LC commands to ERLC command handler
                Config.erlcCommands?.HandleMessage(client, message);
                return;
            }

            // Get guild config if message is sent in a server
            GuildConfig? guildConfig = message.channel?.guild_id is not null ? await WhispCache.GuildConfig.Get(message.channel.guild_id) : null;

            string prefix = guildConfig?.prefix ?? Config.prefix;
            string mention = $"<@{client.readyData?.user.id}>";

            string staffPrefix = Config.staffPrefix;

            if (message.content.StartsWith(mention)) prefix = mention; // Allow <@botid> as prefix

            if (message.content.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                // General command
                await HandleCommand(client, message, prefix);
            }
            else if (message.content.StartsWith(staffPrefix, StringComparison.CurrentCultureIgnoreCase) && await IsStaff(message.author.id))
            {
                // Staff command
                await HandleStaffCommand(client, message, prefix);
            }
        }

        public async Task HandleCommand(Client client, Message message, string prefix)
        {
            List<string> args = [.. message.content[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            string content = args.Join(" ");

            Command? command = GetCommandByName(content, out int length);
            args.RemoveRange(0, length);
            if (command is null) return;

            // Extract flags (args that start with --) from args
            MatchCollection matches = Regex.Matches(args.Join(" "), @"--(\w+)");
            List<string> flags = [.. matches.Select(m => m.Groups[1].Value)];
            args = [.. args.Where(a => !flags.Contains(a))];

            var ctx = new CommandContext(client, message, args, flags);

            if (ctx.GuildConfig is not null && ctx.GuildConfig.version != Config.EnvId) return; // Ignore messages from guilds using different environments

            if (ctx.UserConfig?.ack_required ?? false) // Banned user
            {
                await ctx.Reply(Actions.GenerateAcknowledgeMessage(long.Parse(ctx.UserId ?? "0")));
                return;
            }

            if (await IsRatelimited(ctx, command)) return;

            try
            {
                await command.ExecuteAsync(ctx);
            }
            catch (Exception ex)
            {
                var id = SentrySdk.CaptureException(ex); // Send to Sentry

                Log.Error($"An error occured while executing '{command.Name}'\nUser: @{ctx.User?.username} ({ctx.UserId})\nGuild: {ctx.Guild?.name} ({ctx.GuildId})\n\n", ex);

                await SendErrorMessage(ctx, id);
            }
        }

        public async Task HandleStaffCommand(Client client, Message message, string prefix)
        {
            List<string> args = [.. message.content[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            string content = args.Join(" ");

            Command? command = GetCommandByName(content, out int length);
            args.RemoveRange(0, length);

            // Extract flags (args that start with --) from args
            MatchCollection matches = Regex.Matches(args.Join(" "), @"--(\w+)");
            List<string> flags = [.. matches.Select(m => m.Groups[1].Value)];
            args = [.. args.Where(a => !flags.Contains(a))];

            command?.ExecuteAsync(new CommandContext(client, message, args, flags));
        }

        private bool _hasWarnedMissingEnv = false;
        public async Task<bool> IsStaff(string moderatorId)
        {
            string? supportServerId = Environment.GetEnvironmentVariable("WHISP_SUPPORT_SERVER_ID");
            string? staffRoleId = Environment.GetEnvironmentVariable("WHISP_STAFF_ROLE_ID");

            if (supportServerId is null || staffRoleId is null)
            {
                if (!_hasWarnedMissingEnv)
                {
                    Log.Warning("WHISP_SUPPORT_SERVER_ID or WHISP_STAFF_ROLE_ID environment variables are not set. Staff commands will not work.");
                    _hasWarnedMissingEnv = true;
                }
                return false;
            }

            string[] staffRoleIds = staffRoleId.Split(',');

            Guild? guild = await DiscordCache.Guilds.Get(supportServerId);
            if (guild is null) return false;

            Member? member = await guild.members.Get(moderatorId);
            if (member is null || member.roles is null) return false;

            foreach (string roleId in staffRoleIds)
            {
                if (member.roles.Contains(roleId.Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        public Command? GetCommandByName(string name, out int length)
        {
            Command? command = null;
            int commandLength = 0;
            for (int len = MaxLength; len > 0; len--)
            {
                Command? activeCommand = commands.Find(c =>
                {
                    foreach (string alias in c.Aliases)
                    {
                        commandLength = alias.Split(" ").Length;
                        if (commandLength == len && (name.StartsWith($"{alias} ", StringComparison.CurrentCultureIgnoreCase) || name.Equals(alias, StringComparison.CurrentCultureIgnoreCase)))
                        {
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

            length = commandLength;
            return command;
        }

        public async Task<bool> IsRatelimited(CommandContext ctx, Command command)
        {
            if (command.Ratelimits.Count > 0) return false;

            Message message = ctx.message;

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

                        return true;
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

            return false;
        }

        public async Task SendErrorMessage(CommandContext ctx, SentryId id)
        {
            await ctx.EditResponse(new MessageBuilder
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
