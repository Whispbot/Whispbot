using Newtonsoft.Json;
using Npgsql;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot
{
    public partial class WhispCache
    {
        public static readonly Collection<GuildConfig> GuildConfig = new(async (key, args) =>
        {
            GuildConfig? existingRecord = Postgres.SelectFirst<GuildConfig>(
              @"SELECT * FROM guild_config WHERE id = @1;",
              [long.Parse(key)]
            );

            return existingRecord ?? Postgres.SelectFirst<GuildConfig>(
                @"INSERT INTO guild_config (id, name) VALUES (@1, @2) RETURNING *;",
                [long.Parse(key), DiscordCache.Guilds.Get(key).WaitFor()?.name]
            );
        });

        public static readonly Collection<ShiftConfig> ShiftConfig = new(async (key, args) =>
        {
            return Postgres.SelectFirst<ShiftConfig>(
                @"SELECT * FROM module_shifts WHERE id = @1;", [long.Parse(key)]
            ) ?? new();
        });


        public static readonly Collection<UserConfig> UserConfig = new(async (key, args) =>
        {
            UserConfig? existingRecord = Postgres.SelectFirst<UserConfig>(
              @"SELECT * FROM user_config WHERE id = @1;",
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
                types = [Postgres.SelectFirst<ShiftType>(
                    @"INSERT INTO shift_types (guild_id, is_default) VALUES (@1, true) RETURNING *;",
                    [long.Parse(key)]
                )];
            }

            return types;
        });
    }

    public class GuildConfig
    {
        public long id = 0;
        public string? name;
        public string? icon_url;
        public BotVersion version = BotVersion.Production;
        public long enabled_modules = 0;

        public int? default_language = 0;
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
    }

    public class ERLCServerConfig
    {
        public Guid id;
        public long guild_id = 0;
        public string api_key = "";
        private string? _decrypted_api_key = null;
        public string DecryptedApiKey => _decrypted_api_key ??= Tools.ERLC.DecryptApiKey(api_key);
        public int ingame_players = 0;
        public string? name = null;
        public string? code = null;
    }

    public enum BotVersion
    {
        Production = 0,
        Beta = 1,
        Alpha = 2
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
}
