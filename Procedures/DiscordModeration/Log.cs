using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using Whispbot.Tools.Discord;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Whispbot.DiscordModeration;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        /// <summary>
        /// A dictionary mapping moderation types to a tuple containing the name, action, suffix, color, permissions required and a boolean indicating if an action has a duration
        /// </summary>
        public static Dictionary<DiscordModerationType, (string, string, Color, bool, Permissions, Func<Context, Task<string?>>?)> TypeData = new()
        {
            // Type                            | Name     | Suff. | Color                     | Has Duration
            { DiscordModerationType.Warning,    ("Warn",    "in",   new Color(255, 255, 255),   false,  Permissions.ModerateMembers,    null    ) },
            { DiscordModerationType.Mute,       ("Mute",    "in",   new Color(130, 35,  200),   true,   Permissions.ModerateMembers,    Mute    ) },
            { DiscordModerationType.Unmute,     ("Unmute",  "in",   new Color(85,  170, 85 ),   false,  Permissions.ModerateMembers,    Unmute  ) },
            { DiscordModerationType.Kick,       ("Kick",    "from", new Color(200, 160, 15 ),   false,  Permissions.KickMembers,        Kick    ) },
            { DiscordModerationType.Softban,    ("Softban", "from", new Color(200, 110, 15 ),   false,  Permissions.KickMembers,        Softban ) },
            { DiscordModerationType.Ban,        ("Ban",     "from", new Color(170, 0  , 0  ),   true,   Permissions.BanMembers,         Ban     ) },
            { DiscordModerationType.Unban,      ("Unban",   "from", new Color(85,  170, 85 ),   false,  Permissions.BanMembers,         Unban   ) }
        };

        public static async Task ModerateFromCommand(CommandContext ctx, DiscordModerationType type)
        {
            var context = await GatherContextFromCommand(ctx, type);

            if (context.Error is not null)
            {
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.{context.Error}}}.");
                return;
            }

            var guild = context.Guild;
            var moderator = context.Moderator;
            var target = context.TargetUser;

            if (guild is null || moderator is null || target is null)
            {
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.invalid_ctx}}.");
                return;
            }

            // Make sure user has correct permissions and the module is enabled
            var permissionCheck = await HasPermission(context);
            if (!permissionCheck.Item1)
            {
                await ctx.Reply($"{{emoji.cross}} {permissionCheck.Item2}");
                return;
            }

            var (newCase, transaction) = await CreateCase(context);

            if (newCase is null)
            {
                transaction?.Rollback();
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.failed_create_case}}.");
                return;
            }

            var typeData = TypeData[context.Type!.Value];
            if (typeData.Item6 is not null)
            {
                try
                {
                    var errorMessage = await typeData.Item6(context);

                    if (errorMessage is not null)
                    {
                        transaction?.Rollback();
                        await ctx.Reply($"{{emoji.cross}} {errorMessage}");
                        return;
                    }
                }
                catch
                {
                    transaction?.Rollback();
                    await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.action_failed}}.");
                    return;
                }
            }

            transaction?.Commit();

            var userMessage = await SendUserMessage(newCase);
            _ = Task.Run(() => Log(newCase));

            await ctx.Reply(await GenerateConfirmationMessage(newCase, userMessage is not null));

            var config = await WhispCache.GuildConfig.Get(guild.id);
            if (config?.discord_moderation?.delete_trigger_message ?? true) await ctx.message.Delete();
        }

        /// <summary>
        /// Log a moderation to the server's set log channel.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to log.</param>
        /// <returns>The <see cref="Message"/> that has been sent to the log channel.</returns>
        public static async Task<Message?> Log(DiscordModerationCase log)
        {
            var config = await WhispCache.GuildConfig.Get(log.guild_id.ToString());

            if (config?.discord_moderation?.log_channel_id is null) return null;

            Channel channel = new(config.discord_moderation.log_channel_id.ToString()!);

            return (await channel.Send(await GenerateLogMessage(log))).Item1;
        }

        /// <summary>
        /// Create a moderation case in the database using the given <see cref="Context"/>.
        /// </summary>
        /// <param name="context">The <see cref="Context"/> of the moderation.</param>
        /// <returns>The created <see cref="DiscordModerationCase"/>.</returns>
        public static async Task<(DiscordModerationCase?, Npgsql.NpgsqlTransaction?)> CreateCase(Context context)
        {
            var guildId = long.Parse(context.Guild!.id);
            var moderatorId = long.Parse(context.Moderator!.id);
            var targetId = long.Parse(context.TargetUser!.id);
            var type = (int)context.Type!;
            var reason = context.Reason!;
            var duration_s = context.DurationSeconds;
            DateTimeOffset? expires_at = duration_s is not null && duration_s > 0 ? DateTimeOffset.UtcNow + TimeSpan.FromSeconds((double)duration_s) : null;

            var transaction = Postgres.BeginTransaction();

            int i = 6;
            return (Postgres.SelectFirst<DiscordModerationCase>(
                $"INSERT INTO discord_moderations (guild_id, moderator_id, target_id, type, reason, expires_at, duration_s) VALUES (@1, @2, @3, @4, @5, {(expires_at is not null ? $"@{i++}" : "NULL")}, {(duration_s is not null ? $"@{i++}" : "NULL")}) RETURNING *;",
                [
                    guildId,
                    moderatorId, 
                    targetId,
                    type,
                    reason,
                    ..(expires_at is not null ? new List<object> { expires_at! } : []),
                    ..(duration_s is not null ? new List<object> { duration_s! } : [])],
                transaction
            ), transaction);
        }

        /// <summary>
        /// Generate a <see cref="MessageBuilder"/> containing the log embed for a given <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to generate a log message for.</param>
        /// <returns>The <see cref="MessageBuilder"/> containing the log embed.</returns>
        public static async Task<MessageBuilder> GenerateLogMessage(DiscordModerationCase log)
        {
            var type = TypeData[(DiscordModerationType)log.type];

            var moderator = DiscordCache.Users.Get(log.moderator_id.ToString());
            var target = DiscordCache.Users.Get(log.target_id.ToString());

            var guildConfig = WhispCache.GuildConfig.Get(log.guild_id.ToString());

            Task.WaitAll(moderator, target);

            return new MessageBuilder()
            {
                embeds = [
                    new EmbedBuilder()
                    {
                        thumbnail = target.Result?.avatar_url != null ? new EmbedThumbnail { url = target.Result.avatar_url } : null
                    }
                    .SetAuthor($"@{moderator.Result?.username ?? "unknown"} ({log.moderator_id})", moderator.Result?.avatar_url)
                    .SetDescription(
                        $"{{string.dm.pt.{type.Item1.ToLower()}}} " +
                        $"**@{Users.FixUsername(target.Result?.username ?? "unknown")}** " +
                        $"({log.target_id})" +
                        $"{(type.Item4 ? $" {{string.content.phrase.for}} {Time.ConvertMillisecondsToString((log.duration_s ?? 0) * 1000d)}" : "")}."
                    ) // Fuck locales
                    .AddField(
                        new EmbedField
                        {
                            name = "{string.title.dm.reason}",
                            value = log.reason
                        }
                    )
                    .SetColor(type.Item3)
                    .SetFooter($"{{string.footer.dm.case}}: {log.case_id}")
                ]
            }
            .Process((Strings.Language)((await guildConfig)?.default_language ?? 0), null, true); // Process locales
        }

        /// <summary>
        /// Sends a message to the target user in the given <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to send a message for.</param>
        /// <returns>Returns the <see cref="Message"/> sent to the target user.</returns>
        public static async Task<Message?> SendUserMessage(DiscordModerationCase log)
        {
            var user = await DiscordCache.Users.Get(log.target_id.ToString());
            if (user is null) return null;

            var channel = await user.GetDMChannel();
            if (channel is null) return null;

            return (await channel.Send(await GenerateUserMessage(log))).Item1;
        }

        /// <summary>
        /// Generates a <see cref="MessageBuilder"/> containing the message to be sent to the target user for a given <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to generate a message for.</param>
        /// <returns>Returns the <see cref="Message"/> sent to the target user.</returns>
        public static async Task<MessageBuilder> GenerateUserMessage(DiscordModerationCase log)
        {
            var type = TypeData[(DiscordModerationType)log.type];

            var guild = DiscordCache.Guilds.Get(log.guild_id.ToString());
            var userConfig = WhispCache.UserConfig.Get(log.target_id.ToString());
            var guildConfig = WhispCache.GuildConfig.Get(log.guild_id.ToString());

            var language = (await userConfig)?.language ?? (await guildConfig)?.default_language;

            return new MessageBuilder()
            {
                embeds = [
                    new EmbedBuilder()
                    .SetDescription(
                        $"{{string.dm.content.{(type.Item4 ? "actionduration" : "action")}:" +
                        $"type={{string.dm.prefix.{type.Item1.ToLower()}}}," +
                        $"suffix={{string.dm.suffix.{type.Item2.ToLower()}}}," +
                        $"server={(await guild)?.name ?? "err"}," +
                        $"reason={log.reason}," +
                        $"duration={Time.ConvertMillisecondsToString((log.duration_s ?? 0) * 1000d)}" +
                        $"}}"
                    )
                    .SetColor(type.Item3)
                ]
            }
            .Process((Strings.Language)(language ?? 0), null, false); // Process locales
        }

        /// <summary>
        /// Generates a <seealso cref="string"/> to send after the trigger message for a moderation <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">TThe <see cref="DiscorDModerationCase"/> to generate a message for.</param>
        /// <param name="messagedUser">A <seealso cref="bool"/> indicating whether or not the user recieved a DM regarding their moderation.</param>
        /// <returns>Returns a <seealso cref="string"/> to send back to the moderator.</returns>
        public static async Task<string> GenerateConfirmationMessage(DiscordModerationCase log, bool messagedUser)
        {
            var type = TypeData[(DiscordModerationType)log.type];

            var configTask = WhispCache.GuildConfig.Get(log.guild_id.ToString());
            var modConfigTask = WhispCache.UserConfig.Get(log.moderator_id.ToString());
            var userTask = DiscordCache.Users.Get(log.target_id.ToString());

            var config = await configTask;
            var mod = await modConfigTask;
            var user = await userTask;

            var language = (Strings.Language)(mod?.language ?? config?.default_language ?? 0);

            return
                $"{{emoji.tick}}" +
                $"{((config?.discord_moderation?.display_case_id ?? true) ? $"{{string.content.dm.case}} {log.case_id} - " : "")}" +
                $"{{string.content.phrase.successfully}} {$"{{string.dm.pt.{type.Item1.ToLower()}}}".Process(language).ToLowerInvariant()} **@{user?.username ?? "err"}**" +
                $"{(type.Item4 && log.duration_s is not null ? $" {{string.content.phrase.for}} **{Time.ConvertMillisecondsToString((double)log.duration_s * 1000, ", ", false, 1000, language)}**" : "")}" +
                $"{((config?.discord_moderation?.display_case_reason ?? true) ? $" {{string.content.phrase.for}} **{log.reason}**{(log.reason.EndsWith('.') || !messagedUser ? "" : '.')}" : "")}" +
                $"{(messagedUser ? "" : " - {string.content.dm.messagefailed}.")}";
        }

        /// <summary>
        /// Generates a <see cref="Context"/> object containing all the relevant information for a moderation action based on a Discord <see cref="CommandContext"/>.
        /// </summary>
        /// <param name="ctx">The Discord <see cref="CommandContext"/> from a legacy command.</param>
        /// <param name="type">The <see cref="DiscordModerationType"/> the command relates to.</param>
        /// <returns>Moderation <see cref="Context"/> gathered from the <see cref="CommandContext"/>.</returns>
        public static async Task<Context> GatherContextFromCommand(CommandContext ctx, DiscordModerationType type)
        {
            if (ctx.GuildId is null) return new Context(null, null, null, null, null, null, "not_in_guild");

            var typeData = TypeData[type];

            var userString = ctx.args.FirstOrDefault();
            if (userString is null) return new Context(null, null, null, null, null, null, "no_user");
            ctx.args.RemoveAt(0);

            var user = await Users.GetUserByString(userString, 4, ctx.GuildId); // 4 is the min length to match to prevent people just typing !ban a
            if (user is null) return new Context(null, null, null, null, null, null, "invalid_user");

            long? length = null;
            string reason = "";
            if (typeData.Item4) // The type has a duration
            {
                length = (long)Time.ConvertMessageToMilliseconds(ctx.args.Join(" "), out reason) / 1000;
                if (length == 0) length = null;
            }
            else
            {
                reason = ctx.args.Join(" ");
            }

            var config = ctx.GuildConfig;

            if ((config?.discord_moderation?.require_duration ?? false) && length == 0)
            {
                return new Context(null, null, null, null, null, null, "no_duration");
            }
            else if (length is null)
            {
                if (type == DiscordModerationType.Mute)
                {
                    length = config?.discord_moderation?.default_mute_length_s ?? 600; // Default to 10 minutes
                }
                else if (type == DiscordModerationType.Ban)
                {
                    length = config?.discord_moderation?.default_ban_length_s ?? -1; // Default to permanent
                }
            }

            if ((config?.discord_moderation?.require_reason ?? false) && string.IsNullOrWhiteSpace(reason))
            {
                return new Context(null, null, null, null, null, null, "no_reason");
            }
            else if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "No reason provided";
            }

            return new Context(user, reason, length, ctx.Guild, ctx.User, type, null);
        }

        /// <summary>
        /// Checks if the moderator from the given <see cref="Context"/> has the necessary permissions to perform the moderation action.
        /// </summary>
        /// <param name="context">The generated <see cref="Context"/>.</param>
        /// <returns>A tuple (<seealso cref="bool"/>, <seealso cref="string"/>?) representing whether the moderator has permissions and an error message which is only <seealso cref="null"/> when item1 is <seealso cref="true"/>.</returns>
        public static async Task<(bool, string?)> HasPermission(Context context)
        {
            if (!(await WhispPermissions.CheckModule(context.Guild!.id, Module.DiscordModeration)).Item1) return (false, "{string.errors.dm.moduledisabled}.");

            var typeData = TypeData[context.Type!.Value];

            var ownsServer = context.Guild.owner_id == context.Moderator!.id;
            if (!ownsServer && !(await DiscordPermissions.HasPermissionOrAdmin(context.Guild, context.Moderator!.id, typeData.Item5))) return (false, "{string.errors.dm.nopermissions}.");

            var targetMember = await context.Guild.members.Get(context.TargetUser!.id);
            if (targetMember?.user?.id == context.Guild.owner_id) return (false, "{string.errors.dm.ownercantdie}.");

            var moderatorMember = await context.Guild.members.Get(context.Moderator.id);
            var moderatorRoles = context.Guild.Roles.FindMany((r, id) => moderatorMember?.roles?.Contains(r.id) ?? false);
            var moderatorHighestRole = moderatorRoles.OrderByDescending(r => r.position).FirstOrDefault();
            if (context.Guild.Roles.FindMany((r, _) => targetMember?.roles?.Contains(r.id) ?? false).Find((r) => r.position > moderatorHighestRole?.position) is not null) return (false, "{string.errors.dm.targetbetter}.");

            return (true, null);
        }

        public record Context(User? TargetUser, string? Reason, long? DurationSeconds, Guild? Guild, User? Moderator, DiscordModerationType? Type, string? Error);
    }

    public class DiscordModerationCase
    {
        public int case_id;
        public long guild_id;
        public long moderator_id;
        public long target_id;
        public int type;
        public string reason = "No reason provided";
        public DateTimeOffset created_at;
        public DateTimeOffset? updated_at;
        public DateTimeOffset? expires_at;
        public int? duration_s;
        public long? updated_by;
        public bool is_deleted;

        public long? message_id;
        public long? dm_message_id;
    }

    public enum DiscordModerationType
    {
        Warning,
        Mute,
        Unmute,
        Kick,
        Softban,
        Ban,
        Unban
    }
}
