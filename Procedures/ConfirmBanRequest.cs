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
        public static async Task<Message?> GetBanRequestLogMessage(BanRequest banRequest)
        {
            if (banRequest.message_id is null) return null;

            GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(banRequest.guild_id.ToString());
            if (guildConfig is null) return null;

            long? logChannelId = guildConfig.roblox_moderation?.ban_request_channel_id;
            if (logChannelId is null) return null;

            return new()
            {
                id = banRequest.message_id.ToString() ?? "",
                channel_id = logChannelId.ToString() ?? ""
            };
        }

        public static async Task PostModifyBanRequest(BanRequest banRequest)
        {
            Message? logMessage = await GetBanRequestLogMessage(banRequest);
            if (logMessage is null) return;

            await logMessage.Edit(await GetBanRequestMessage(banRequest));
        }

        public static async Task PostRemoveBanRequest(BanRequest banRequest)
        {
            Message? logMessage = await GetBanRequestLogMessage(banRequest);
            if (logMessage is null) return;

            await logMessage.Delete("Request completed");
        }

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

        public static async Task SendBanRequestCommand(BanRequest banRequest, ERLCServerConfig erlcServer)
        {
            var initialMessageUpdate = PostModifyBanRequest(banRequest);

            var result = await ERLC.SendCommand(erlcServer, $":ban {banRequest.target_id}");

            if (result?.code == ERLC.ErrorCode.Success)
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
                    [result?.message ?? "{string.errors.rmbr.unknownerror}", banRequest.id]
                );
                await initialMessageUpdate;
                if (newRequest is not null) await PostModifyBanRequest(newRequest);
            }
        }

        public static async Task<(BanRequest?, string?)> DeleteBanRequest(long id, long guildId, long moderatorId)
        {
            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.ManageBanRequests))
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

        public static async Task<(BanRequest?, string?)> MarkAsBanned(long id, long guildId, long moderatorId)
        {
            if (!(await WhispPermissions.CheckModule(guildId.ToString(), Commands.Module.RobloxModeration)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.ManageBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(guildId.ToString());
            RobloxModerationType? banType = types?.FirstOrDefault(t => t.is_ban_type && !t.is_deleted);
            if (banType is null)
            {
                return (null, "{string.errors.rmbr.nobantype}");
            }

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

                var moderation = await CreateModeration(guildId, moderatorId, banRequest.target_id, banType, $"[Requested] {banRequest.reason}");
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

        public static async Task<(BanRequest?, string?)> ApproveBanRequest(long id, long guildId, long moderatorId, ERLCServerConfig erlcServer)
        {
            if (!(await WhispPermissions.CheckModule(guildId.ToString(), Commands.Module.RobloxModeration | Commands.Module.ERLC)).Item1) return (null, "{string.errors.rmlog.moduledisabled}");

            if (erlcServer.api_key is null) return (null, "{string.errors.rmbr.noapikey}");

            if (!await WhispPermissions.HasPermission(guildId.ToString(), moderatorId.ToString(), BotPermissions.ManageBanRequests))
            {
                return (null, "{string.errors.rmlog.noperms}");
            }

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(guildId.ToString());
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
