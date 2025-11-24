using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot
{
    public static partial class Procedures
    {
        public static async Task PostCreateModeration(RobloxModeration moderation)
        {
            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(moderation.guild_id.ToString());
            if (guildConfig is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id.ToString());
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            long? logChannelId = type?.log_channel_id ?? guildConfig.roblox_moderation_default_log_channel_id;

            if (logChannelId is not null)
            {
                Channel? logChannel = await DiscordCache.Channels.Get(logChannelId.ToString()!);
                if (logChannel is not null)
                {
                    var (log, _) = await logChannel.Send(
                        await GetRMLogMessage(moderation)
                    );

                    if (log is not null)
                    {
                        Postgres.Execute(
                            @"
                        UPDATE roblox_moderations
                        SET message_id = @1
                        WHERE guild_id = @2 AND " + "\"case\"" + @" = @3;
                        ",
                            [long.Parse(log.id), moderation.guild_id, moderation.@case]
                        );
                    }
                }
            }
        }

        public static async Task<MessageBuilder> GetRMLogMessage(RobloxModeration moderation)
        {

            User? moderator = await DiscordCache.Users.Get(moderation.moderator_id.ToString());
            Roblox.RobloxUser? target = await Roblox.GetUserById(moderation.target_id.ToString());

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(moderation.guild_id.ToString());
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(moderation.guild_id.ToString());

            return new MessageBuilder
            {
                embeds = [
                    new EmbedBuilder
                    {
                        author = new EmbedAuthor
                        {
                            name = moderator?.global_name is not null ? moderator.global_name : $"@{moderator?.username ?? "unknown"}",
                            icon_url = moderator?.avatar_url
                        },
                        title = "{string.title.rmlog.newmoderation}",
                        thumbnail = new EmbedThumbnail
                        {
                            url = await Roblox.GetUserAvatar(moderation.target_id)
                        },
                        fields = [
                            new EmbedField
                            {
                                name = "{string.title.rmlog.user}",
                                value = $"{{emoji.user}} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{{emoji.chat}} {target?.displayName}\n" : "")}{{emoji.folder}} {target?.id}\n{{emoji.clock}} <t:{target?.createTime?.ToUnixTimeSeconds()}:d> (<t:{target?.createTime?.ToUnixTimeSeconds()}:R>)"
                            },
                            new EmbedField
                            {
                                name = "{string.title.rmlog.type}",
                                value = $"{{emoji.moderation}} {type?.name ?? "Unknown Type"}"
                            },
                            new EmbedField
                            {
                                name = "{string.title.rmlog.reason}",
                                value = $"{{emoji.alignment}} {moderation.reason ?? "*{string.content.rmlog.noreason}.*"}"
                            }
                        ],
                        footer = new EmbedFooter
                        {
                            text = $"{{string.content.rmlog.case}}: {moderation.@case}"
                        }
                    }
                ],
                components = [
                    new ActionRowBuilder
                    {
                        components = [
                            new ButtonBuilder
                            {
                                custom_id = $"rm_log_editreason {moderation.guild_id} {moderation.@case}",
                                style = ButtonStyle.Secondary,
                                emoji = Strings.GetEmoji("pen"),
                                label = "{string.button.rmlog.editreason}"
                            },
                            new ButtonBuilder
                            {
                                custom_id = $"rm_log_edittype {moderation.guild_id} {moderation.@case}",
                                style = ButtonStyle.Secondary,
                                emoji = Strings.GetEmoji("folder"),
                                label = "{string.button.rmlog.edittype}"
                            },
                            new ButtonBuilder
                            {
                                custom_id = $"rm_log_delete {moderation.guild_id} {moderation.@case}",
                                style = ButtonStyle.Danger,
                                emoji = Strings.GetEmoji("delete")
                            }
                        ]
                    }
                ]
            }.Process((Strings.Language)(guildConfig?.default_language ?? 0), null, true);
        }

        public static async Task<(RobloxModeration?, string?)> CreateModeration(long guildId, long moderatorId, long targetId, RobloxModerationType type, string reason = null, int flags = 0)
        {
            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.UseRobloxModerations))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            if (type.is_deleted)
            {
                return (null, "{string.errors.rmlog.typedeleted}");
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
            } return (null, "{string.errors.rmlog.logfailed}");
        }

        public static async Task<(RobloxModeration?, string?)> CreateModeration(string guildId, string moderatorId, string targetId, RobloxModerationType type, string raeson = null, int flags = 0)
        {
            return await CreateModeration(long.Parse(guildId), long.Parse(moderatorId), long.Parse(targetId), type, raeson, flags);
        }
    }

    public class RobloxModeration
    {
        [JsonProperty("case")]
        public int @case;
        public long guild_id;
        public long moderator_id;
        public long target_id;
        public Guid type;
        public string? reason;
        public RobloxModerationFlags flags;
        public DateTimeOffset created_at;
        public DateTimeOffset updated_at;
        public long? updated_by;
        public long? message_id;
    }

    public enum RobloxModerationFlags
    {

    }
}
