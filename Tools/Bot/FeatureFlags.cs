using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot.Tools.Bot
{
    public static class FeatureFlags
    {
        public static readonly Dictionary<string, FeatureFlag?> Flags = [];

        public static async Task<FeatureFlag?> FetchFlagByName(string name)
        {
            FeatureFlag? flag = Postgres.SelectFirst<FeatureFlag>(
                "SELECT * FROM feature_flags WHERE name = @1 LIMIT 1",
                [name]
            );

            if (flag is null)
            {
                Log.Warning($"Feature flag '{name}' could not be found");
            }

            Flags[name] = flag;

            return flag;
        }

        public static FeatureFlag? CheckCacheForFlag(string name)
        {
            bool _ = Flags.TryGetValue(name, out FeatureFlag? flag);
            return flag;
        }

        public static async Task<FeatureFlag?> GetFlagByName(string name)
        {
            FeatureFlag? flag = CheckCacheForFlag(name);

            if (flag is not null) return flag;

            flag = await FetchFlagByName(name);

            Flags[name] = flag;

            return flag;
        }

        public static async Task<bool> CheckGlobalFlag(string name)
        {
            FeatureFlag? flag = await GetFlagByName(name);
            if (flag is null) return false;

            if (flag.Affect != FeatureFlagAffect.Global) return false;

            return flag.enabled;
        }

        public static async Task<bool> CheckGuildFlag(string name, long guildId)
        {
            FeatureFlag? flag = await GetFlagByName(name);
            if (flag is null) return false;
            if (!flag.enabled) return false;

            if (flag.Affect != FeatureFlagAffect.Guild) return false;

            if (flag.Type == FeatureFlagType.Automatic)
            {
                DateTimeOffset start = flag.start_at ?? DateTimeOffset.MinValue;
                DateTimeOffset finish = flag.finish_at ?? DateTimeOffset.MaxValue;
                float percentageFinished = (float)(DateTimeOffset.UtcNow - start).TotalSeconds / (float)(finish - start).TotalSeconds;
                int guildHash = (int)(guildId % 100);
                return guildHash <= (percentageFinished * 100);
            }
            else
            {
                GuildConfig? guildConfig = await WhispCache.GuildConfig.Get(guildId.ToString());
                if (guildConfig is null) return false;

                return guildConfig.feature_flags.Contains(name);
            }
        }

        public static async Task<bool> CheckUserFlag(string name, long userId)
        {
            FeatureFlag? flag = await GetFlagByName(name);
            if (flag is null) return false;
            if (!flag.enabled) return false;

            if (flag.Affect != FeatureFlagAffect.User) return false;

            if (flag.Type == FeatureFlagType.Automatic)
            {
                DateTimeOffset start = flag.start_at ?? DateTimeOffset.MinValue;
                DateTimeOffset finish = flag.finish_at ?? DateTimeOffset.MaxValue;
                float percentageFinished = (float)(DateTimeOffset.UtcNow - start).TotalSeconds / (float)(finish - start).TotalSeconds;
                int userHash = (int)(userId % 100);
                return userHash <= (percentageFinished * 100);
            }
            else
            {
                UserConfig? userConfig = await WhispCache.UserConfig.Get(userId.ToString());
                if (userConfig is null) return false;

                return userConfig.feature_flags.Contains(name);
            }
        }

        public class FeatureFlag
        {
            public Guid id;
            /// <summary>
            /// TRUE = Manual, FALSE = Automatic
            /// </summary>
            public bool type = true;
            public FeatureFlagType Type => type ? FeatureFlagType.Manual : FeatureFlagType.Automatic;
            /// <summary>
            /// NULL = Global, TRUE = Guild, FALSE = User
            /// </summary>
            public bool? affects = null;
            public FeatureFlagAffect Affect => affects is null ? FeatureFlagAffect.Global : (affects.Value ? FeatureFlagAffect.Guild : FeatureFlagAffect.User);
            /// <summary>
            /// Lower snake case name
            /// </summary>
            public string name = "new_feature";
            public string description = "";
            public bool enabled = false;
            [JsonProperty("public")]
            public bool @public = false;
            public long created_by;
            public DateTimeOffset created_at;
            public long? updated_by;
            public DateTimeOffset updated_at;
            public DateTimeOffset? start_at;
            public DateTimeOffset? finish_at;
        }

        public class FeatureFlagUpdate
        {
            public byte status = 0;
        }

        public enum FeatureFlagType
        {
            Manual,
            Automatic
        }

        public enum FeatureFlagAffect
        {
            Global,
            Guild,
            User
        }
    }
}
