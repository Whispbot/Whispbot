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
using Whispbot.Tools.Disc;

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
                    .WithTitle(Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.title.new"))
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(banRequest.target_id.ToString()))
                    .WithFields([
                        new EmbedFieldBuilder()
                            .WithName(Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.field.user"))
                            .WithValue($"{Emojis.Get("user")} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{Emojis.Get("chat")} {target?.displayName}\n" : "")}{Emojis.Get("folder")} {target?.id}\n{Emojis.Get("clock")} <t:{target?.CreateTime?.ToUnixTimeSeconds()}:d> (<t:{target?.CreateTime?.ToUnixTimeSeconds()}:R>)"),
                        new EmbedFieldBuilder()
                            .WithName(Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.field.reason"))
                            .WithValue($"{Emojis.Get("alignment")} {banRequest.reason ?? $"*{Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.no_reason")}.*"}"),
                        ..(banRequest.status is not null ? new List<EmbedFieldBuilder> {
                            new EmbedFieldBuilder()
                                .WithName(Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.title.status"))
                                .WithValue(banRequest.status == true ? $"{Emojis.Get("loading")} {Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.sending")}..." : $"{Emojis.Get("alignment")} {banRequest.status_message ?? Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.errors.unknown")}")
                        } : [])
                    ])
                    .WithFooter($"ID: {banRequest.id}")
                    .Build(),
                new ComponentBuilder()
                    .AddRow(
                        new ActionRowBuilder()
                            .WithButton(
                                (erlcServers?.Count ?? 0) > 0 ? Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.button.approve") : Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.button.mark_banned"),
                                $"rm_br_confirm {banRequest.id}", 
                                ButtonStyle.Success, 
                                Emojis.Get("tick"), 
                                disabled: banRequest.status == true
                            )
                            .WithButton(
                                Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.requests.button.deny"),
                                $"rm_br_deny {banRequest.id}",
                                ButtonStyle.Danger,
                                Emojis.Get("delete"),
                                disabled: banRequest.status == true
                            )
                    )
                    .Build()
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
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.RobloxModeration)).Item1) return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.errors.module_disabled"));

            // Check if the moderator can use ban requests
            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.UseBanRequests))
            {
                return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.no_permissions"));
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
            return (null, Whispbot.Languages.Translator.Get(Whispbot.Languages.Language.EnglishUK, "rmod.log.errors.failed"));
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
