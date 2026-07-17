using Newtonsoft.Json;
using Npgsql;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Tools;
using static Whispbot.Cache.WhispCache;

namespace Whispbot.Databases
{
    public static class UpdateHandler
    {
        public static async Task ListenForUpdates()
        {
            int i = 0;
            while (!Postgres.IsConnected() && i < 10)
            {
                Thread.Sleep(5000);
                i++;
            }

            using var conn = Postgres.GetConnection();
            if (conn is null)
            {
                Log.Error("Notifcation listner connection failed");
                return;
            }

            conn.Notification += async (o, e) =>
            {
                try
                {
                    if (e.Channel == "guild_update")
                    {
                        var data = JsonConvert.DeserializeObject<GuildUpdatePayload>(e.Payload);

                        if (data is null) return;

                        if (data.table == "guild_config" || data.table.StartsWith("module_", StringComparison.InvariantCultureIgnoreCase))
                        {
                            GuildConfig? newConfig = await WhispCache.GuildConfig.Fetch(data.id);
                            if (newConfig is null)
                            {
                                WhispCache.GuildConfig.Remove(data.id);
                            }
                        }
                        else if (data.table == "shift_types")
                        {
                            List<ShiftType>? newTypes = await ShiftTypes.Fetch(data.id);
                        }
                        else if (data.table == "roblox_moderation_types")
                        {
                            List<RobloxModerationType>? newTypes = await RobloxModerationTypes.Fetch(data.id);
                        }
                        else if (data.table == "erlc_servers")
                        {
                            List<ERLCServerConfig>? newServers = await ERLCServerConfigs.Fetch(data.id);
                        }
                        else if (data.table == "permission_roles")
                        {
                            List<PermissionRole>? newRoles = await WhispPermissions.permissionRoles.Fetch(data.id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                    Log.Error(ex, $"An error occured while updating data. ID: {ex}");
                }
            };

            using var listenGuildUpdate = new NpgsqlCommand("LISTEN guild_update;", conn);
            listenGuildUpdate.ExecuteNonQuery();

            while (true) await conn.WaitAsync();
        }

#pragma warning disable IDE1006
        public record GuildUpdatePayload(ulong id, string table, string op);
        public record ProofDeletePayload(string id, string guild_id, string extension);
#pragma warning restore IDE1006
    }
}
