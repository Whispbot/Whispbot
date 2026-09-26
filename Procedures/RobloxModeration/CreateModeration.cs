using Discord;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Logging;

namespace Whispbot
{
    public static partial class Procedures
    {
        /// <summary>
        /// Posts a moderation log message and updates the moderation with the message ID
        /// </summary>
        /// <param name="moderation">The new moderation</param>
        /// <returns></returns>
        private static async Task PostCreateModeration(RobloxModeration moderation)
        {
            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(moderation.guild_id);
            if (guildConfig is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            ulong? logChannelId = type?.log_channel_id ?? guildConfig.roblox_moderation?.default_log_channel_id;

            if (logChannelId is not null)
            {
                var guild = Config.client!.GetGuild(moderation.guild_id);
                var logChannel = guild.GetTextChannel(logChannelId.Value);
                if (logChannel is not null)
                {
                    var (embed, components) = await GetRMLogMessage(moderation);

                    var message = await logChannel.SendMessageAsync(embed: embed, components: components);

                    if (message is not null)
                    {
                        Postgres.Execute( // Update moderation with log message ID
                            @"
                        UPDATE roblox_moderations
                        SET message_id = @1
                        WHERE guild_id = @2 AND " + "\"case\"" + @" = @3;
                        ",
                            [message.Id, moderation.guild_id, moderation.@case]
                        );
                    }
                }
            }
        }
        
        /// <summary>
        /// Creates a rm log message for a case
        /// </summary>
        /// <param name="moderation">The case to make the message for</param>
        /// <returns><see cref="MessageBuilder"/> with the log message</returns>
        public static async Task<(Embed, MessageComponent)> GetRMLogMessage(RobloxModeration moderation)
        {
            IUser? moderator = await Config.client!.GetUserAsync(moderation.moderator_id, CacheMode.AllowDownload, RequestOptions.Default);
            Roblox.RobloxUser? target = await Roblox.GetUserById(moderation.target_id.ToString());

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(moderation.guild_id);
            var lang = guildConfig?.default_language ?? 0;

            Logger.WithData(target ?? new()).Debug(target?.CreateTime?.ToUnixTimeSeconds().ToString() ?? "bruh");

            return (
                new EmbedBuilder()
                    .WithAuthor(moderator.GlobalName ?? $"@{moderator.Username}", moderator.GetDisplayAvatarUrl(), $"{Config.websiteUrl}/case/{moderation.guild_id}/{moderation.@case}")
                    .WithTitle(lang.Translate("rmod.log.title"))
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(moderation.target_id.ToString()))
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName(lang.Translate("rmod.log.field.user"))
                            .WithValue($"{Emojis.Get("user")} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{Emojis.Get("chat")} {target?.displayName}\n" : "")}{Emojis.Get("folder")} {target?.id}\n{Emojis.Get("clock")} <t:{target?.CreateTime?.ToUnixTimeSeconds()}:d> (<t:{target?.CreateTime?.ToUnixTimeSeconds()}:R>)"),
                        new EmbedFieldBuilder()
                            .WithName(lang.Translate("rmod.log.field.type"))
                            .WithValue($"{Emojis.Get("moderation")} {type?.name ?? lang.Translate("rmod.log.errors.unknown_type")}"),
                        new EmbedFieldBuilder()
                            .WithName(lang.Translate("rmod.log.field.reason"))
                            .WithValue($"{Emojis.Get("alignment")} {moderation.reason ?? lang.Translate("rmod.log.errors.no_reason")}")
                    )
                    .WithFooter($"{lang.Translate("rmod.log.footer")}: {moderation.@case}")
                    .Build(),
                new ComponentBuilder()
                    .AddRow(
                        new ActionRowBuilder()
                            .WithButton(lang.Translate("rmod.log.button.reason"), $"rm_log_editreason {moderation.@case}", ButtonStyle.Secondary, Emojis.Get("pen"))
                            .WithButton(lang.Translate("rmod.log.button.type"), $"rm_log_edittype {moderation.@case}", ButtonStyle.Secondary, Emojis.Get("folder"))
                            .WithButton(lang.Translate("rmod.log.button.delete"), $"rm_log_delete {moderation.@case}", ButtonStyle.Danger, Emojis.Get("delete"))
                    )
                    .Build()
            );
        }

        /// <summary>
        /// Create a Roblox moderation case
        /// </summary>
        /// <param name="guildId">The guild this case is in</param>
        /// <param name="moderatorId">The moderator creating the case</param>
        /// <param name="targetId">The roblox user being moderated</param>
        /// <param name="type">The type of moderation (changes per guild - use <see cref="WhispCache.RobloxModerationTypes"/>)</param>
        /// <param name="reason">The reason behind the case</param>
        /// <param name="flags">Flags for the moderation (1 - from ban request)</param>
        /// <returns>(<see cref="RobloxModeration?"/>, <see cref="string?"/>) where item1 is the new case and item2 is the error if failed to create</returns>
        public static async Task<(RobloxModeration?, string?)> CreateModeration(ulong guildId, ulong moderatorId, ulong targetId, RobloxModerationType type, string reason = "No reason provided", int flags = 0)
        {
            // Makes sure the rm module is enabled
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.RobloxModeration)).Item1) return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.errors.module_disabled"));

            // Makes sure the moderator has permissions to create the case
            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseRobloxModerations))
            {
                return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.no_permissions"));
            }

            if (type.is_deleted) // Don't create cases with deleted types
            {
                return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.type_deleted"));
            }

            RobloxModeration? moderation = Postgres.SelectFirst<RobloxModeration>(
                @"
                INSERT INTO roblox_moderations (guild_id, moderator_id, target_id, type, reason, flags)
                VALUES (@1, @2, @3, @4, @5, @6)
                RETURNING *;
                ",
                [guildId, moderatorId, targetId, type.id, reason, flags]
            );

            if (moderation is not null)
            {
                _ = Task.Run(() => PostCreateModeration(moderation));
                return (moderation, null);
            } return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.failed"));
        }

        public static async Task<(RobloxModeration?, string?)> CreateModeration(string guildId, string moderatorId, string targetId, RobloxModerationType type, string raeson = "No reason provided", int flags = 0)
        {
            return await CreateModeration(ulong.Parse(guildId), ulong.Parse(moderatorId), ulong.Parse(targetId), type, raeson, flags);
        }
    }

    public class RobloxModeration
    {
        [JsonProperty("case")]
        public int @case;
        public ulong guild_id;
        public ulong moderator_id;
        public ulong target_id;
        public Guid type;
        public string? reason;
        public RobloxModerationFlags flags;
        public DateTimeOffset created_at;
        public DateTimeOffset updated_at;
        public ulong? updated_by;
        public ulong? message_id;
    }

    public enum RobloxModerationFlags
    {
    }
}
