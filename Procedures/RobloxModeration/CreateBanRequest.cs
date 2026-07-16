using Discord;
using Microsoft.AspNetCore.Mvc.Formatters;
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
using Whispbot.Tools;

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
            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id);
            if (guildConfig is null) return;

            ulong? logChannelId = guildConfig.roblox_moderation?.ban_request_channel_id;

            if (logChannelId is not null)
            {
                IGuild guild = Config.client!.GetGuild(banRequest.guild_id);
                ITextChannel logChannel = await guild.GetTextChannelAsync(logChannelId.Value);
                if (logChannel is not null)
                {
                    var (embed, components) = await GetBanRequestMessage(banRequest);

                    var log = await logChannel.SendMessageAsync(
                        embed: embed,
                        components: components
                    );

                    if (log is not null)
                    {
                        Postgres.Execute(
                            @"
                            UPDATE ban_requests
                            SET message_id = @1
                            WHERE id = @2;
                            ",
                            [log.Id, banRequest.id]
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
        public static async Task<(Embed, MessageComponent)> GetBanRequestMessage(BanRequest banRequest)
        {
            IUser? moderator = await Config.client!.GetUserAsync(banRequest.moderator_id, CacheMode.AllowDownload, RequestOptions.Default);
            Roblox.RobloxUser? target = await Roblox.GetUserById(banRequest.target_id.ToString());

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id);

            // Get ERLC Servers that allow ban requests, if 0 then we just mark as banned instead of approving
            List<ERLCServerConfig>? erlcServers = (await WhispCache.ERLCServerConfigs.Get(banRequest.guild_id))?.Where(s => s.allow_ban_requests)?.ToList();

            return (
                new EmbedBuilder()
                    .WithAuthor(moderator.GlobalName ?? $"@{moderator.Username}", moderator.GetDisplayAvatarUrl())
                    .WithTitle("{string.title.rmbr.newrequest}")
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(banRequest.target_id.ToString()))
                    .WithFields([
                        new EmbedFieldBuilder()
                            .WithName("{string.title.rmlog.user}")
                            .WithValue($"{{emoji.user}} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{{emoji.chat}} {target?.displayName}\n" : "")}{{emoji.folder}} {target?.id}\n{{emoji.clock}} <t:{target?.createTime?.ToUnixTimeSeconds()}:d> (<t:{target?.createTime?.ToUnixTimeSeconds()}:R>)"),
                        new EmbedFieldBuilder()
                            .WithName("{string.title.rmlog.reason}")
                            .WithValue($"{{emoji.alignment}} {banRequest.reason ?? "*{string.content.rmlog.noreason}.*"}"),
                        ..(banRequest.status is not null ? new List<EmbedFieldBuilder> {
                            new EmbedFieldBuilder()
                                .WithName("{string.title.rmbr.status}")
                                .WithValue(banRequest.status == true ? "{emoji.loading} {string.content.rmbr.sending}..." : $"{{emoji.alignment}} {banRequest.status_message ?? "{string.errors.rmbr.unknownerror}"}")
                        } : [])
                    ])
                    .WithFooter($"ID: {banRequest.id}")
                    .Build()
                    .ProcessObj((Strings.Language)(guildConfig?.default_language ?? 0))!,
                new ComponentBuilder()
                    .AddRow(
                        new ActionRowBuilder()
                            .WithButton(
                                (erlcServers?.Count ?? 0) > 0 ? "{string.button.rmbr.approve}" : "{string.button.rmbr.markbanned}",
                                $"rm_br_confirm {banRequest.id}", 
                                ButtonStyle.Success, 
                                Strings.GetEmoji("tick"), 
                                disabled: banRequest.status == true
                            )
                            .WithButton(
                                "{string.button.rmbr.deny}",
                                $"rm_br_deny {banRequest.id}",
                                ButtonStyle.Danger,
                                Strings.GetEmoji("delete"),
                                disabled: banRequest.status == true
                            )
                    )
                    .Build()
                    .ProcessObj((Strings.Language)(guildConfig?.default_language ?? 0))!
            );
        }

        /// <summary>
        /// Creates a ban request log
        /// </summary>
        /// <param name="guildId">The server the request is in</param>
        /// <param name="moderatorId">The moderator making the request</param>
        /// <param name="targetId">The roblox user being banned</param>
        /// <param name="reason">The reason for the request</param>
        /// <returns>(<see cref="BanRequest?"/>, <see cref="string?"/>) where item1 is the new ban request and item2 is the error if failed</returns>
        public static async Task<(BanRequest?, string?)> CreateBanRequest(ulong guildId, ulong moderatorId, ulong targetId, string reason = "No reason provided")
        {
            // Check if the module is even enabled
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.RobloxModeration)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            // Check if the moderator can use ban requests
            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseBanRequests))
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
            return await CreateBanRequest(ulong.Parse(guildId), ulong.Parse(moderatorId), ulong.Parse(targetId), raeson);
        }
    }

    public class BanRequest
    {
        public ulong id;
        public ulong guild_id;
        public ulong moderator_id;
        public ulong target_id;
        public string? reason;
        public DateTimeOffset created_at;
        public ulong? message_id;
        /// <summary>
        /// NULL - Pending
        /// True - Approved
        /// False - Failed
        /// </summary>
        public bool? status;
        public string? status_message;
    }
}
