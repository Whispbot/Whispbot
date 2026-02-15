using Microsoft.AspNetCore.Mvc.Formatters;
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
        /// <summary>
        /// Send the ban request log to the log channel and update ban request with message ID
        /// </summary>
        /// <param name="banRequest">The new ban request</param>
        /// <returns></returns>
        public static async Task PostCreateBanRequest(BanRequest banRequest)
        {
            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id.ToString());
            if (guildConfig is null) return;

            long? logChannelId = guildConfig.roblox_moderation?.ban_request_channel_id;

            if (logChannelId is not null)
            {
                Channel? logChannel = await DiscordCache.Channels.Get(logChannelId.ToString()!);
                if (logChannel is not null)
                {
                    var (log, _) = await logChannel.Send(
                        await GetBanRequestMessage(banRequest)
                    );

                    if (log is not null)
                    {
                        Postgres.Execute(
                            @"
                            UPDATE ban_requests
                            SET message_id = @1
                            WHERE id = @2;
                            ",
                            [long.Parse(log.id), banRequest.id]
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Generate a log message for a ban request
        /// </summary>
        /// <param name="banRequest">The ban request to create the log for</param>
        /// <returns><see cref="MessageBuilder"/> of the log</returns>
        public static async Task<MessageBuilder> GetBanRequestMessage(BanRequest banRequest)
        {
            User? moderator = await DiscordCache.Users.Get(banRequest.moderator_id.ToString());
            Roblox.RobloxUser? target = await Roblox.GetUserById(banRequest.target_id.ToString());

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id.ToString());

            // Get ERLC Servers that allow ban requests, if 0 then we just mark as banned instead of approving
            List<ERLCServerConfig>? erlcServers = (await WhispCache.ERLCServerConfigs.Get(banRequest.guild_id.ToString()))?.Where(s => s.allow_ban_requests)?.ToList();

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
                        title = "{string.title.rmbr.newrequest}",
                        thumbnail = new EmbedThumbnail
                        {
                            url = await Roblox.GetUserAvatar(banRequest.target_id)
                        },
                        fields = [
                            new EmbedField
                            {
                                name = "{string.title.rmlog.user}",
                                value = $"{{emoji.user}} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{{emoji.chat}} {target?.displayName}\n" : "")}{{emoji.folder}} {target?.id}\n{{emoji.clock}} <t:{target?.createTime?.ToUnixTimeSeconds()}:d> (<t:{target?.createTime?.ToUnixTimeSeconds()}:R>)"
                            },
                            new EmbedField
                            {
                                name = "{string.title.rmlog.reason}",
                                value = $"{{emoji.alignment}} {banRequest.reason ?? "*{string.content.rmlog.noreason}.*"}"
                            },
                            ..(
                            banRequest.status is not null ?
                                new List<EmbedField> {
                                    new()
                                    {
                                        name = "{string.title.rmbr.status}",
                                        value = banRequest.status == true ? "{emoji.loading} {string.content.rmbr.sending}..." : $"{{emoji.alignment}} {banRequest.status_message ?? "{string.errors.rmbr.unknownerror}"}"
                                    }
                                } :
                                []
                            )
                        ],
                        footer = new EmbedFooter
                        {
                            text = $"ID: {banRequest.id}"
                        }
                    }
                ],
                components = [
                    new ActionRowBuilder
                    {
                        components = [
                            new ButtonBuilder
                            {
                                custom_id = $"rm_br_confirm {banRequest.id}",
                                style = ButtonStyle.Success,
                                emoji = Strings.GetEmoji("tick"),
                                label = (erlcServers?.Count ?? 0) > 0 ? "{string.button.rmbr.approve}" : "{string.button.rmbr.markbanned}",
                                disabled = banRequest.status == true
                            },
                            new ButtonBuilder
                            {
                                custom_id = $"rm_br_deny {banRequest.id}",
                                style = ButtonStyle.Danger,
                                emoji = Strings.GetEmoji("delete"),
                                label = "{string.button.rmbr.deny}",
                                disabled = banRequest.status == true
                            }
                        ]
                    }
                ]
            }.Process((Strings.Language)(guildConfig?.default_language ?? 0), null, true);
        }

        /// <summary>
        /// Creates a ban request log
        /// </summary>
        /// <param name="guildId">The server the request is in</param>
        /// <param name="moderatorId">The moderator making the request</param>
        /// <param name="targetId">The roblox user being banned</param>
        /// <param name="reason">The reason for the request</param>
        /// <returns>(<see cref="BanRequest?"/>, <see cref="string?"/>) where item1 is the new ban request and item2 is the error if failed</returns>
        public static async Task<(BanRequest?, string?)> CreateBanRequest(long guildId, long moderatorId, long targetId, string reason = "No reason provided")
        {
            // Check if the module is even enabled
            if (!(await WhispPermissions.CheckModule(guildId.ToString(), Commands.Module.RobloxModeration)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            // Check if the moderator can use ban requests
            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.UseBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            BanRequest? banRequest = Postgres.SelectFirst<BanRequest>(
                @"
                INSERT INTO ban_requests (guild_id, moderator_id, target_id, reason)
                VALUES (@1, @2, @3, @4)
                RETURNING *;
                ",
                [guildId, moderatorId, targetId, reason]
            );

            if (banRequest is not null)
            {
                _ = Task.Run(() => PostCreateBanRequest(banRequest));
                return (banRequest, null);
            }
            return (null, "{string.errors.rmlog.logfailed}");
        }

        /// <summary>
        /// Creates a ban request log
        /// </summary>
        /// <param name="guildId">The server the request is in</param>
        /// <param name="moderatorId">The moderator making the request</param>
        /// <param name="targetId">The roblox user being banned</param>
        /// <param name="reason">The reason for the request</param>
        /// <returns>(<see cref="BanRequest?"/>, <see cref="string?"/>) where item1 is the new ban request and item2 is the error if failed</returns>
        public static async Task<(BanRequest?, string?)> CreateBanRequest(string guildId, string moderatorId, string targetId, string raeson = "No reason provided")
        {
            return await CreateBanRequest(long.Parse(guildId), long.Parse(moderatorId), long.Parse(targetId), raeson);
        }
    }

    public class BanRequest
    {
        public long id;
        public long guild_id;
        public long moderator_id;
        public long target_id;
        public string? reason;
        public DateTimeOffset created_at;
        public long? message_id;
        /// <summary>
        /// NULL - Pending
        /// True - Approved
        /// False - Failed
        /// </summary>
        public bool? status;
        public string? status_message;
    }
}
