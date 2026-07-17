using Discord;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Whispbot.DiscordModeration;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        /// <summary>
        /// A dictionary mapping moderation types to a tuple containing the name, action, suffix, color, permissions required and a boolean indicating if an action has a duration
        /// </summary>p
        public static Dictionary<DiscordModerationType, (string, string, Color, bool, GuildPermission, Func<Context, Task<string?>>?)> TypeData = new()
        {
            // Type                            | Name     | Suff. | Color                     | Has Duration
            { DiscordModerationType.Warning,    ("Warn",    "in",   new Color(255, 255, 255),   false,  GuildPermission.ModerateMembers,    null    ) },
            { DiscordModerationType.Mute,       ("Mute",    "in",   new Color(130, 35,  200),   true,   GuildPermission.ModerateMembers,    Mute    ) },
            { DiscordModerationType.Unmute,     ("Unmute",  "in",   new Color(85,  170, 85 ),   false,  GuildPermission.ModerateMembers,    Unmute  ) },
            { DiscordModerationType.Kick,       ("Kick",    "from", new Color(200, 160, 15 ),   false,  GuildPermission.KickMembers,        Kick    ) },
            { DiscordModerationType.Softban,    ("Softban", "from", new Color(200, 110, 15 ),   false,  GuildPermission.KickMembers,        Softban ) },
            { DiscordModerationType.Ban,        ("Ban",     "from", new Color(170, 0  , 0  ),   true,   GuildPermission.BanMembers,         Ban     ) },
            { DiscordModerationType.Unban,      ("Unban",   "from", new Color(85,  170, 85 ),   false,  GuildPermission.BanMembers,         Unban   ) }
        };

        public static async Task ModerateFromCommand(CommandContext ctx, DiscordModerationType type)
        {
            var context = await GatherContextFromCommand(ctx, type);

            // This section of code is super jank because it was designed to have the permission check after the error check
            // But people were complaining about how the error check was before the permission check
            // So I had to move stuff around and change up some code to avoid null reference errors
            var permissionCheck = await HasPermission(
                context.Guild!,
                context.Moderator!,
                context.TargetUser, 
                (DiscordModerationType)context.Type!
            );
            if (!permissionCheck.Item1)
            {
                await ctx.Reply($"{{emoji.cross}} {permissionCheck.Item2}", true);
                return;
            }

            if (context.Error is not null)
            {
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.{context.Error}}}.", true);
                return;
            }

            var guild = context.Guild;
            var moderator = context.Moderator;
            var target = context.TargetUser;

            if (guild is null || moderator is null || target is null)
            {
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.invalid_ctx}}.", true);
                return;
            }

            var (newCase, transaction) = await CreateCase(context);

            if (newCase is null)
            {
                transaction?.Rollback();
                await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.failed_create_case}}.", true);
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
                        await ctx.Reply($"{{emoji.cross}} {errorMessage}", true);
                        return;
                    }
                }
                catch
                {
                    transaction?.Rollback();
                    await ctx.Reply($"{{emoji.cross}} {{string.errors.dm.action_failed}}.", true);
                    return;
                }
            }

            transaction?.Commit();

            var userMessage = await SendUserMessage(newCase);
            _ = Task.Run(() => Log(newCase));

            var config = await WhispCache.GuildConfig.Get(guild.Id);

            await ctx.Reply(await GenerateConfirmationMessage(newCase, userMessage is not null), config?.discord_moderation?.delete_trigger_message ?? true);

            if (config?.discord_moderation?.delete_trigger_message ?? true)
            {
                if (ctx.type == CommandType.Legacy && ctx.message is not null)
                {
                    await ctx.message.DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Log a moderation to the server's set log channel.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to log.</param>
        /// <returns>The <see cref="Message"/> that has been sent to the log channel.</returns>
        public static async Task<RestUserMessage?> Log(DiscordModerationCase log)
        {
            var config = await WhispCache.GuildConfig.Get(log.guild_id);

            if (config?.discord_moderation?.log_channel_id is null) return null;

            var guild = Config.client!.GetGuild(log.guild_id);
            var channel = guild.GetTextChannel(config.discord_moderation.log_channel_id.Value);

            var message = await channel.SendMessageAsync(embed: await GenerateLogEmbed(log));

            if (message is not null)
            {
                Postgres.Execute(
                    "UPDATE discord_moderations SET message_id = @1 WHERE case_id = @2;",
                    [message.Id, log.case_id]
                );
            }

            return message;
        }

        /// <summary>
        /// Create a moderation case in the database using the given <see cref="Context"/>.
        /// </summary>
        /// <param name="context">The <see cref="Context"/> of the moderation.</param>
        /// <returns>The created <see cref="DiscordModerationCase"/>.</returns>
        public static async Task<(DiscordModerationCase?, Npgsql.NpgsqlTransaction?)> CreateCase(Context context)
        {
            var guildId = context.Guild!.Id;
            var moderatorId = context.Moderator!.Id;
            var targetId = context.TargetUser!.Id;
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
        public static async Task<Embed> GenerateLogEmbed(DiscordModerationCase log)
        {
            var type = TypeData[(DiscordModerationType)log.type];

            var moderatorTask = Config.client!.GetUserAsync(log.moderator_id, CacheMode.AllowDownload, RequestOptions.Default);
            var targetTask = Config.client!.GetUserAsync(log.target_id, CacheMode.AllowDownload, RequestOptions.Default);

            var moderator = await moderatorTask;
            var target = await targetTask;

            var guildConfig = WhispCache.GuildConfig.Get(log.guild_id);

            return new EmbedBuilder()
                .WithAuthor($"@{moderator.Username} ({log.moderator_id})", moderator.GetDisplayAvatarUrl())
                .WithThumbnailUrl(target.GetDisplayAvatarUrl())
                .WithDescription(
                    $"{{string.dm.pt.{type.Item1.ToLower()}}} " +
                    $"**@{Users.FixUsername(target.Username)}** " +
                    $"({log.target_id})" +
                    $"{(type.Item4 ? $" {{string.content.phrase.for}} {Time.ConvertMillisecondsToString((log.duration_s ?? 0) * 1000d)}" : "")}."
                ) // Fuck locales
                .AddField("{string.title.dm.reason}", log.reason)
                .WithColor(type.Item3)
                .WithFooter($"{{string.footer.dm.case}}: {log.case_id}")
                .Build();
        }

        /// <summary>
        /// Sends a message to the target user in the given <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to send a message for.</param>
        /// <returns>Returns the <see cref="Message"/> sent to the target user.</returns>
        public static async Task<IUserMessage?> SendUserMessage(DiscordModerationCase log)
        {
            var user = await Config.client!.GetUserAsync(log.target_id, CacheMode.AllowDownload, RequestOptions.Default);
            if (user is null) return null;

            var channel = await user.CreateDMChannelAsync();
            if (channel is null) return null;

            var message = await channel.SendMessageAsync(embed: await GenerateUserEmbed(log));

            if (message is not null)
            {
                Postgres.Execute(
                    "UPDATE discord_moderations SET dm_message_id = @1 WHERE case_id = @2;",
                    [message.Id, log.case_id]
                );
            }

            return message;
        }

        /// <summary>
        /// Generates a <see cref="MessageBuilder"/> containing the message to be sent to the target user for a given <see cref="DiscordModerationCase"/>.
        /// </summary>
        /// <param name="log">The <see cref="DiscordModerationCase"/> to generate a message for.</param>
        /// <returns>Returns the <see cref="Message"/> sent to the target user.</returns>
        public static async Task<Embed> GenerateUserEmbed(DiscordModerationCase log)
        {
            var type = TypeData[(DiscordModerationType)log.type];

            var guild = Config.client!.GetGuild(log.guild_id);
            var userConfig = WhispCache.UserConfig.Get(log.target_id);
            var guildConfig = WhispCache.GuildConfig.Get(log.guild_id);

            var language = (await userConfig)?.language ?? (await guildConfig)?.default_language;

            return new EmbedBuilder()
                .WithDescription(
                    $"{{string.dm.content.{(type.Item4 ? "actionduration" : "action")}:" +
                    $"type={{string.dm.prefix.{type.Item1.ToLower()}}}," +
                    $"suffix={{string.dm.suffix.{type.Item2.ToLower()}}}," +
                    $"server={guild.Name}," +
                    $"reason={log.reason}," +
                    $"duration={Time.ConvertMillisecondsToString((log.duration_s ?? 0) * 1000d)}" +
                    $"}}"
                )
                .WithColor(type.Item3)
                .Build()
                !; // Process locales
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

            var configTask = WhispCache.GuildConfig.Get(log.guild_id);
            var modConfigTask = WhispCache.UserConfig.Get(log.moderator_id);
            var userTask = Config.client!.GetUserAsync(log.target_id, CacheMode.AllowDownload, RequestOptions.Default);

            var config = await configTask;
            var mod = await modConfigTask;
            var user = await userTask;

            var language = mod?.language ?? config?.default_language ?? 0;

            return
                $"{{emoji.tick}}" +
                $"{((config?.discord_moderation?.display_case_id ?? true) ? $"{{string.content.dm.case}} {log.case_id} - " : "")}" +
                $"{{string.content.phrase.successfully}} {$"{{string.dm.pt.{type.Item1.ToLower()}}}".Translate(language).ToLowerInvariant()} **@{user.Username ?? "err"}**" +
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
            var typeData = TypeData[type];

            var user = ctx.args.Get("user")?.GetUser();
            if (user is null) return new Context(user, null, null, ctx.Guild, ctx.User, type, "invalid_user");

            long? length = null;
            string? reason;
            if (typeData.Item4) // The type has a duration
            {
                var arg = ctx.args.Get("duration");
                length = (long)(arg?.GetDuration()?.TotalMilliseconds ?? 0);
                reason = arg?.GetString() ?? ctx.args.Get("reason")?.GetString();
                if (length == 0) length = null;
            }
            else
            {
                reason = ctx.args.Get("reason")?.GetString();
            }

            var config = ctx.GuildConfig;

            if ((config?.discord_moderation?.require_duration ?? false) && length == 0)
            {
                return new Context(user, reason, length, ctx.Guild, ctx.User, type, "no_duration");
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
                return new Context(user, reason, length, ctx.Guild, ctx.User, type, "no_reason");
            }
            else if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "*No reason provided.*";
            }

            return new Context(user, reason, length, ctx.Guild, ctx.User, type, null);
        }

        /// <summary>
        /// Checks if the moderator from the given <see cref="Context"/> has the necessary permissions to perform the moderation action.
        /// </summary>
        /// <param name="context">The generated <see cref="Context"/>.</param>
        /// <returns>A tuple (<seealso cref="bool"/>, <seealso cref="string"/>?) representing whether the moderator has permissions and an error message which is only <seealso cref="null"/> when item1 is <seealso cref="true"/>.</returns>
        public static async Task<(bool, string?)> HasPermission(IGuild guild, IUser moderator, IUser? target, DiscordModerationType type)
        {
            if (!(await WhispPermissions.CheckModule(guild.Id, Module.DiscordModeration)).Item1) return (false, "{string.errors.dm.moduledisabled}.");

            var typeData = TypeData[type];

            var ownsServer = guild.OwnerId == moderator.Id;
            if (!ownsServer && !(await DiscordPermissions.HasPermissionOrAdmin(guild, moderator.Id, typeData.Item5))) return (false, "{string.errors.dm.nopermissions}.");

            if (target is null) return (false, "{string.errors.dm.no_user}");
            var targetMember = await guild.GetUserAsync(target.Id);
            if (targetMember.Id == guild.OwnerId) return (false, "{string.errors.dm.ownercantdie}.");

            var moderatorMember = await guild.GetUserAsync(moderator.Id);
            var moderatorRoles = guild.Roles.Where((r, id) => moderatorMember?.RoleIds?.Contains(r.Id) ?? false);
            var moderatorHighestRole = moderatorRoles.OrderByDescending(r => r.Position).FirstOrDefault();
            if (guild.Roles.Where((r, _) => targetMember?.RoleIds?.Contains(r.Id) ?? false).Any((r) => r.Position > moderatorHighestRole?.Position)) return (false, "{string.errors.dm.targetbetter}.");

            return (true, null);
        }

        public record Context(IUser? TargetUser, string? Reason, long? DurationSeconds, IGuild? Guild, IUser? Moderator, DiscordModerationType? Type, string? Error);
    }

    public class DiscordModerationCase
    {
        public int case_id;
        public ulong guild_id;
        public ulong moderator_id;
        public ulong target_id;
        public int type;
        public string reason = "No reason provided";
        public DateTimeOffset created_at;
        public DateTimeOffset? updated_at;
        public DateTimeOffset? expires_at;
        public int? duration_s;
        public ulong? updated_by;
        public bool is_deleted;

        public ulong? message_id;
        public ulong? dm_message_id;
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
