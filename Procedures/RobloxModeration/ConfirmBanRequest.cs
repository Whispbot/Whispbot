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
using Whispbot.Tools;
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot
{
    public static partial class Procedures
    {
        /// <summary>
        /// Creates a message object representing the log message for a ban request without making any API calls
        /// </summary>
        /// <param name="banRequest">The ban request to get the message for</param>
        /// <returns>The <see cref="Message"/> or null if failed to get appropriate data</returns>
        public static async Task<ITextChannel?> GetBanRequestLogChannel(BanRequest banRequest)
        {
            if (banRequest.message_id is null) return null;

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id);
            if (guildConfig is null) return null;

            ulong? logChannelId = guildConfig.roblox_moderation?.ban_request_channel_id;
            if (logChannelId is null) return null;

            var guild = Config.client!.GetGuild(banRequest.guild_id);
            if (guild == null) return null;

            return guild.GetTextChannel(logChannelId.Value);
        }

        /// <summary>
        /// Updates the log message after modifying it
        /// </summary>
        /// <param name="banRequest">The modified ban request</param>
        /// <returns></returns>
        public static async Task PostModifyBanRequest(BanRequest banRequest)
        {
            if (banRequest.message_id is null) return;

            var logChannel = await GetBanRequestLogChannel(banRequest);
            if (logChannel is null) return;

            var (embed, components) = await GetBanRequestMessage(banRequest);
            await logChannel.ModifyMessageAsync(banRequest.message_id.Value, m => { m.Embed = embed; m.Components = components; });
        }

        /// <summary>
        /// Deletes the log message after being completed
        /// </summary>
        /// <param name="banRequest">The deleted ban request</param>
        /// <returns></returns>
        public static async Task PostRemoveBanRequest(BanRequest banRequest)
        {
            if (banRequest.message_id is null) return;

            var logChannel = await GetBanRequestLogChannel(banRequest);
            if (logChannel is null) return;

            await logChannel.DeleteMessageAsync(banRequest.message_id.Value);
        }

        /// <summary>
        /// Update the ban request as failed with the given error message  
        /// </summary>
        /// <param name="banRequest">The failed ban request</param>
        /// <param name="error">The error provided from the API</param>
        /// <returns></returns>
        public static async Task OnBanRequestFail(BanRequest banRequest, string error)
        {
            BanRequest? updatedRequest = Postgres.SelectFirst<BanRequest>(
                @"
                UPDATE ban_requests
                SET status = FALSE, status_message = @1
                WHERE id = @2
                RETURNING *",
                [error, banRequest.id]
            );

            if (updatedRequest is not null) await PostModifyBanRequest(updatedRequest);
        }

        /// <summary>
        /// Sends the ban command to the ERLC server and updates the ban request (and log) to reflect that
        /// </summary>
        /// <param name="banRequest">The request that has been approved</param>
        /// <param name="erlcServer">The ERLC server to send the request to</param>
        /// <returns></returns>
        public static async Task SendBanRequestCommand(BanRequest banRequest, ERLCServerConfig erlcServer)
        {
            var initialMessageUpdate = PostModifyBanRequest(banRequest);

            var result = await ERLCAPI.SendCommand(erlcServer, $":ban {banRequest.target_id}");

            if (result?.error == ErrorCode.Unknown)
            {
                await initialMessageUpdate;
                await MarkAsBanned(banRequest.id, banRequest.guild_id, banRequest.moderator_id);
            }
            else
            {
                BanRequest? newRequest = Postgres.SelectFirst<BanRequest>(
                    @"
                    UPDATE ban_requests
                    SET status = FALSE, status_message = @1
                    WHERE id = @2
                    RETURNING *",
                    [result?.error_message ?? "{string.errors.rmbr.unknownerror}", banRequest.id]
                );
                await initialMessageUpdate;

                if (newRequest is not null) await PostModifyBanRequest(newRequest);
            }
        }

        /// <summary>
        /// Delete a ban request that has been denied
        /// </summary>
        /// <param name="id">The id of the ban request</param>
        /// <param name="guildId">The guild the ban request is from</param>
        /// <param name="moderatorId">The moderator who denied the ban request</param>
        /// <returns>(<see cref="BanRequest?"/>, <see cref="string?"/>) where item1 is the deleted ban request and item2 is the error if failed</returns>
        public static async Task<(BanRequest?, string?)> DeleteBanRequest(ulong id, ulong guildId, ulong moderatorId)
        {
            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            BanRequest? banRequest = Postgres.SelectFirst<BanRequest>(
                @"
                DELETE FROM ban_requests
                WHERE id = @1 AND guild_id = @2
                RETURNING *",
                [id, guildId]
            );

            if (banRequest is not null)
            {
                _ = Task.Run(() => PostRemoveBanRequest(banRequest));
                return (banRequest, null);
            }
            return (null, "{string.errors.rmlog.logfailed}");
        }

        /// <summary>
        /// Mark a ban request as completed and create the corresponding moderation
        /// </summary>
        /// <param name="id"></param>
        /// <param name="guildId"></param>
        /// <param name="moderatorId"></param>
        /// <returns></returns>
        public static async Task<(BanRequest?, string?)> MarkAsBanned(ulong id, ulong guildId, ulong moderatorId)
        {
            // Makes sure the module is actually enabled
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.RobloxModeration)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            // Makes sure the moderator has permission to do this
            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            // Get the ban type for the server 
            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(guildId);
            RobloxModerationType? banType = types?.FirstOrDefault(t => t.is_ban_type && !t.is_deleted);
            if (banType is null)
            {
                return (null, "{string.errors.rmbr.nobantype}");
            }

            // Transaction to make sure that both operations complete successfully
            using var transaction = Postgres.BeginTransaction();
            if (transaction is null) return (null, "{string.errors.general.dbfailed}");

            BanRequest? banRequest = Postgres.SelectFirst<BanRequest>(
                @"
                DELETE FROM ban_requests
                WHERE id = @1 AND guild_id = @2
                RETURNING *",
                [id, guildId],
                transaction
            );

            if (banRequest is not null)
            {
                _ = Task.Run(() => PostRemoveBanRequest(banRequest));

                var moderation = await CreateModeration(guildId, moderatorId, banRequest.target_id, banType, $"[Requested] {banRequest.reason}", 1);
                if (moderation.Item1 is not null)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.Rollback();
                }

                return (banRequest, null);
            }
            return (null, "{string.errors.rmlog.logfailed}");
        }

        /// <summary>
        /// Send the ban command to the ERLC server and mark the ban request as approved
        /// </summary>
        /// <param name="id">The ID of the ban request to approve</param>
        /// <param name="guildId">The guild the ban request is in</param>
        /// <param name="moderatorId">The moderator which approved the ban request</param>
        /// <param name="erlcServer">The server to send the command to</param>
        /// <returns>(<see cref="BanRequest?"/>, <see cref="string?"/>) where item1 is the ban request that has been approved and item2 is the error if failed</returns>
        public static async Task<(BanRequest?, string?)> ApproveBanRequest(ulong id, ulong guildId, ulong moderatorId, ERLCServerConfig erlcServer)
        {
            // Checks if the module is actually enabled
            if (!(await WhispPermissions.CheckModule(guildId, Commands.Module.RobloxModeration | Commands.Module.ERLC)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            if (erlcServer.api_key is null) return (null, "{string.errors.rmbr.noapikey}");

            if (!await WhispPermissions.HasPermission(guildId, moderatorId, BotPermissions.ManageBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            // Get the ban type for the server
            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(guildId);
            RobloxModerationType? banType = types?.FirstOrDefault(t => t.is_ban_type && !t.is_deleted);
            if (banType is null)
            {
                return (null, "{string.errors.rmbr.nobantype}");
            }

            if (erlcServer.allow_ban_requests != true)
            {
                return (null, "{string.errors.rmbr.banrequestsdisabled}");
            }

            BanRequest? banRequest = Postgres.SelectFirst<BanRequest>(
                @"
                UPDATE ban_requests
                SET status = TRUE
                WHERE id = @1 AND guild_id = @2 AND status IS NOT TRUE
                RETURNING *",
                [id, guildId]
            );

            if (banRequest is not null)
            {
                _ = Task.Run(() => SendBanRequestCommand(banRequest, erlcServer));
                return (banRequest, null);
            }
            return (null, "{string.errors.rmbr.alreadyapproved}");
        }
    }
}
