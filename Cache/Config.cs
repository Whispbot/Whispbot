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
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot
{
    public partial class WhispCache
    {
        public static readonly Collection<GuildConfig> GuildConfig = new(async (key, args) =>
        {
            GuildConfig? existingRecord = Postgres.SelectFirst<GuildConfig>(
              @"SELECT 
                    gc.*,
                    to_jsonb(mrm) AS roblox_moderation,
                    to_jsonb(ms) AS shifts,
                    COALESCE(jsonb_agg(ff.name), '[]'::jsonb) AS feature_flags
                FROM guild_config gc 
                LEFT JOIN module_roblox_moderation mrm ON gc.id = mrm.id
                LEFT JOIN module_shifts ms ON gc.id = ms.id
                LEFT JOIN guild_feature_flags gff ON gff.guild_id = gc.id
                LEFT JOIN feature_flags ff ON ff.id = gff.feature_flag_id
                WHERE gc.id = @1;",
              [long.Parse(key)]
            );

            return existingRecord ?? Postgres.SelectFirst<GuildConfig>(
                @"INSERT INTO guild_config (id, name) VALUES (@1, @2) RETURNING *;",
                [long.Parse(key), DiscordCache.Guilds.Get(key).Result?.name ?? ""]
            );
        });

        public static readonly Collection<UserConfig> UserConfig = new(async (key, args) =>
        {
            UserConfig? existingRecord = Postgres.SelectFirst<UserConfig>(
              @"SELECT 
                    uc.*,
                    COALESCE(jsonb_agg(ff.name), '[]'::jsonb) AS feature_flags
                FROM user_config uc
                LEFT JOIN user_feature_flags uff ON uff.user_id = uc.id
                LEFT JOIN feature_flags ff ON ff.id = uff.feature_flag_id
                WHERE id = @1;",
              [long.Parse(key)]
            );

            return existingRecord ?? Postgres.SelectFirst<UserConfig>(
                @"INSERT INTO user_config (id) VALUES (@1) RETURNING *;",
                [long.Parse(key)]
            );
        });

        public static readonly Collection<List<ERLCServerConfig>> ERLCServerConfigs = new(async (key, args) =>
        {
            return Postgres.Select<ERLCServerConfig>(
                @"SELECT * FROM erlc_servers WHERE guild_id = @1;",
                [long.Parse(key)]
            );
        });

        public static readonly Collection<List<ShiftType>> ShiftTypes = new(async (key, args) =>
        {
            List<ShiftType>? types = Postgres.Select<ShiftType>(
                @"SELECT * FROM shift_types WHERE guild_id = @1;",
                [long.Parse(key)]
            );

            if (types is not null && types.Count == 0)
            {
                ShiftType? defaultType = Postgres.SelectFirst<ShiftType>(
                    @"INSERT INTO shift_types (guild_id, is_default) VALUES (@1, true) RETURNING *;",
                    [long.Parse(key)]
                );

                if (defaultType is not null)
                {
                    types.Add(defaultType);
                }
            }

            return types;
        });

        public static readonly Collection<List<RobloxModerationType>> RobloxModerationTypes = new(async (key, args) =>
        {
            List<RobloxModerationType>? types = Postgres.Select<RobloxModerationType>(
                @"SELECT * FROM roblox_moderation_types WHERE guild_id = @1;",
                [long.Parse(key)]
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
                    [long.Parse(key)]
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
        public long id = 0;
        public string? name;
        public string? icon_url;
        public EnvironmentType version = EnvironmentType.Prod;
        public List<string> feature_flags = [];
        public long enabled_modules = 0;
        public string? prefix;

        public int? default_language = 0;

        public ModuleRobloxModeration? roblox_moderation;
        public ModuleShifts? shifts;
    }

    public class ModuleRobloxModeration
    {
        public long? default_log_channel_id;
        public bool require_reason = false;
        public long? ban_request_channel_id;
    }

    public class ModuleShifts
    {
        public long? default_log_channel_id;
    }

    public class ShiftConfig
    {
        public long id = 0;

        public long? default_log_channel_id = null;
    }

    public class UserConfig
    {
        public long id = 0;
        public long? roblox_id = null;
        public DateTimeOffset created_at = DateTimeOffset.MinValue;
        public int? language = 0;
        public bool ack_required = false;

        public List<string> feature_flags = [];
    }

    public class ERLCServerConfig
    {
        public Guid id;
        public long guild_id = 0;
        public bool is_default = false;
        public string api_key = "";
        private string? _decrypted_api_key = null;
        public string DecryptedApiKey => _decrypted_api_key ??= Tools.ERLC.DecryptApiKey(api_key);
        public int ingame_players = 0;
        public string? name = null;
        public string? code = null;

        public bool allow_ban_requests = true;
    }

    public class ShiftType
    {
        public long id = 0;
        public long guild_id = 0;
        public string name = "New Shift Type";
        public bool is_default = false;
        public DateTimeOffset created_at = DateTimeOffset.UtcNow;
        public DateTimeOffset updated_at = DateTimeOffset.UtcNow;
        public bool is_deleted = false;
        public List<string> triggers = [];
        public long? role_id = null;
        public long? log_channel_id = null;
        public List<string>? required_roles = [];
    }

    public class RobloxModerationType
    {
        public Guid id;
        public long guild_id;
        public string name = "New Moderation Type";
        public bool is_deleted = false;
        public List<string> triggers = [];
        public bool is_kick_type = false;
        public bool is_ban_type = false;
        public long? log_channel_id;
        public List<string>? required_roles;
        public DateTimeOffset created_at = DateTimeOffset.UtcNow;
        public DateTimeOffset updated_at = DateTimeOffset.UtcNow;
    }
}
