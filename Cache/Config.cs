using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Npgsql;
using Serilog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Languages;
using Whispbot.Tools;

namespace Whispbot.Cache
{
    public partial class WhispCache
    {
        public static readonly Collection<ulong, GuildConfig> GuildConfig = new(async (key) =>
        {
            using var _ = Tracer.Start("FetchGuildConfig");

            try
            {
                GuildConfig? existingRecord = Postgres.SelectFirst<GuildConfig>(
                  @"SELECT
                      gc.*,
                      to_jsonb(mrm) AS roblox_moderation,
                      to_jsonb(ms) AS shifts,
                      to_jsonb(mdm) AS discord_moderation,
                      COALESCE(
                        jsonb_agg(DISTINCT ff.name) FILTER (WHERE ff.name IS NOT NULL),
                        '[]'::jsonb
                      ) AS feature_flags
                    FROM guild_config gc
                    LEFT JOIN module_roblox_moderation mrm ON gc.id = mrm.id
                    LEFT JOIN module_shifts ms ON gc.id = ms.id
                    LEFT JOIN module_discord_moderation mdm ON gc.id = mdm.id
                    LEFT JOIN guild_feature_flags gff ON gff.guild_id = gc.id
                    LEFT JOIN feature_flags ff ON ff.id = gff.feature_flag_id
                    WHERE gc.id = @1
                    GROUP BY gc.id, mrm.id, ms.id, mdm.id;",
                  [key]
                );

                return existingRecord ?? Postgres.SelectFirst<GuildConfig>(
                    @"INSERT INTO guild_config (id) VALUES (@1) RETURNING *;",
                    [key]
                );
            } 
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch guild config...");
                return null;
            }
        });

        public static readonly Collection<ulong, UserConfig> UserConfig = new(async (key) =>
        {
            using var _ = Tracer.Start("FetchUserConfig");

            UserConfig? existingRecord = Postgres.SelectFirst<UserConfig>(
              @"SELECT 
                    uc.*,
                    COALESCE(jsonb_agg(ff.name), '[]'::jsonb) AS feature_flags
                FROM user_config uc
                LEFT JOIN user_feature_flags uff ON uff.user_id = uc.id
                LEFT JOIN feature_flags ff ON ff.id = uff.feature_flag_id
                WHERE uc.id = @1;",
              [key]
            );

            return existingRecord ?? Postgres.SelectFirst<UserConfig>(
                @"INSERT INTO user_config (id) VALUES (@1) RETURNING *;",
                [key]
            );
        });

        public static readonly Collection<ulong, List<ERLCServerConfig>> ERLCServerConfigs = new(async (key) =>
        {
            using var _ = Tracer.Start("FetchERLCServerConfigs");

            return Postgres.Select<ERLCServerConfig>(
                @"SELECT * FROM erlc_servers WHERE guild_id = @1;",
                [key]
            );
        });

        public static readonly Collection<ulong, List<ShiftType>> ShiftTypes = new(async (key) =>
        {
            using var _ = Tracer.Start("FetchShiftTypes");

            List<ShiftType>? types = Postgres.Select<ShiftType>(
                @"SELECT * FROM shift_types WHERE guild_id = @1;",
                [key]
            );

            if (types is not null && types.Count == 0)
            {
                ShiftType? defaultType = Postgres.SelectFirst<ShiftType>(
                    @"INSERT INTO shift_types (guild_id, is_default) VALUES (@1, true) RETURNING *;",
                    [key]
                );

                if (defaultType is not null)
                {
                    types.Add(defaultType);
                }
            }

            return types;
        });

        public static readonly Collection<ulong, List<RobloxModerationType>> RobloxModerationTypes = new(async (key) =>
        {
            using var _ = Tracer.Start("FetchRobloxModerationTypes");

            List<RobloxModerationType>? types = Postgres.Select<RobloxModerationType>(
                @"SELECT * FROM roblox_moderation_types WHERE guild_id = @1;",
                [key]
            );

            if (types is not null && types.Count == 0)
            {
                List<RobloxModerationType>? defaultTypes = Postgres.Select<RobloxModerationType>(
                    @"
                    INSERT INTO roblox_moderation_types (guild_id, name, triggers, is_kick_type, is_ban_type)
                    VALUES ( @1, 'Warning', '{w,warning,warn}', false, false ),
                           ( @1, 'Kick',    '{k,kick}',         true,  false ),
                           ( @1, 'Ban',     '{b,ban}',          false, true  )
                    ",
                    [key]
                );

                if (defaultTypes is not null)
                {
                    types.AddRange(defaultTypes);
                }
            }

            return types;
        });
    }

    public class GuildConfig
    {
        public ulong id = 0;
        public string? name;
        public string? icon_url;
        public EnvironmentType version = EnvironmentType.Prod;
        public List<string> feature_flags = [];
        public ulong enabled_modules = 0;
        public string? prefix;

        public Language? default_language = 0;

        public ModuleRobloxModeration? roblox_moderation;
        public ModuleShifts? shifts;
        public ModuleDiscordModeration? discord_moderation;
    }

    public class ModuleRobloxModeration
    {
        public ulong? default_log_channel_id;
        public bool require_reason = false;
        public ulong? ban_request_channel_id;
    }

    public class ModuleShifts
    {
        public ulong? default_log_channel_id;
    }

    public class ModuleDiscordModeration
    {
        public ulong? log_channel_id;

        public bool display_case_id = true;
        public bool display_case_reason = true;
        public bool delete_trigger_message = true;

        public int default_mute_length_s = 600;
        public int default_ban_length_s = -1;
        public int delete_messages_duration_s = 3600;

        public bool require_reason = true;
        public bool require_duration = false;

        public bool log_external_moderations = true;
    }

    public class UserConfig
    {
        public ulong id = 0;
        public ulong? roblox_id = null;
        public DateTimeOffset created_at = DateTimeOffset.MinValue;
        public Language? language = 0;
        public bool ack_required = false;

        public List<string> feature_flags = [];
    }

    public class ERLCServerConfig
    {
        public Guid id;
        public ulong guild_id = 0;
        public bool is_default = false;
        public string api_key = "";
        public string internal_id = "";
        public int ingame_players = 0;
        public string? name = null;
        public string? code = null;

        public bool allow_ban_requests = true;
    }

    public class ShiftType
    {
        public ulong id = 0;
        public ulong guild_id = 0;
        public string name = "New Shift Type";
        public bool is_default = false;
        public DateTimeOffset created_at = DateTimeOffset.UtcNow;
        public DateTimeOffset updated_at = DateTimeOffset.UtcNow;
        public bool is_deleted = false;
        public List<string> triggers = [];
        public ulong? role_id = null;
        public ulong? log_channel_id = null;
        public List<ulong>? required_roles = [];
    }

    public class RobloxModerationType
    {
        public Guid id;
        public ulong guild_id;
        public string name = "New Moderation Type";
        public bool is_deleted = false;
        public List<string> triggers = [];
        public bool is_kick_type = false;
        public bool is_ban_type = false;
        public ulong? log_channel_id;
        public List<ulong>? required_roles;
        public DateTimeOffset created_at = DateTimeOffset.UtcNow;
        public DateTimeOffset updated_at = DateTimeOffset.UtcNow;
    }
}
