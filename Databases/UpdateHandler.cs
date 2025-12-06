using Newtonsoft.Json;
using Npgsql;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using static Whispbot.WhispCache;

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
                if (e.Channel == "guild_update")
                {
                    var data = JsonConvert.DeserializeObject<GuildUpdatePayload>(e.Payload);

                    if (data is null) return;

                    if (data.table == "guild_config" || data.table.StartsWith("module_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        GuildConfig? newConfig = await WhispCache.GuildConfig.Fetch(data.id.ToString());
                        if (newConfig is null)
                        {
                            WhispCache.GuildConfig.Remove(data.id.ToString());
                        }
                    }
                    else if (data.table == "shift_types")
                    {
                        List<ShiftType>? newTypes = await ShiftTypes.Fetch(data.id.ToString());
                    }
                    else if (data.table == "roblox_moderation_types")
                    {
                        List<RobloxModerationType>? newTypes = await RobloxModerationTypes.Fetch(data.id.ToString());
                    }
                    else if (data.table == "erlc_servers")
                    {
                        List<ERLCServerConfig>? newServers = await ERLCServerConfigs.Fetch(data.id.ToString());
                    }
                }
                else if (e.Channel == "language_update")
                {
                    var data = JsonConvert.DeserializeObject<LanguageUpdatePayload>(e.Payload);

                    if (data is null) return;

                    if (data.op == "DELETE")
                    {
                        if (!Strings.LanguageStrings.TryGetValue(data.data.language, out Dictionary<string, string>? value)) return;
                        value.Remove(data.data.key);
                    }
                    else
                    {
                        if (!Strings.LanguageStrings.TryGetValue(data.data.language, out var lang))
                        {
                            Strings.LanguageStrings.Add(data.data.language, []);
                            lang = Strings.LanguageStrings[data.data.language];
                        }

                        lang.Remove(data.data.key);
                        lang.Add(data.data.key, data.data.content);
                    }
                }
                else if (e.Channel == "proof_delete" && Config.cluster == 0 && Config.EnvId == 0)
                {
                    var data = JsonConvert.DeserializeObject<ProofDeletePayload>(e.Payload);

                    if (data is null) return;

                    await Bucket.DeleteObject($"guild/{data.guild_id}/moderation/proof/{data.id}.{data.extension}");
                }
            };

            using var listenGuildUpdate = new NpgsqlCommand("LISTEN guild_update;", conn);
            listenGuildUpdate.ExecuteNonQuery();

            using var listenLanguageUpdate = new NpgsqlCommand("LISTEN language_update", conn);
            listenLanguageUpdate.ExecuteNonQuery();

            using var listenProofDelete = new NpgsqlCommand("LISTEN proof_delete", conn);
            listenProofDelete.ExecuteNonQuery();

            while (true) await conn.WaitAsync();
        }

#pragma warning disable IDE1006
        public record GuildUpdatePayload(long id, string table, string op);
        public record ProofDeletePayload(Guid id, string guild_id, string extension);
        public record LanguageUpdatePayload(Strings.DBLanguage data, string op);
#pragma warning restore IDE1006
    }
}
