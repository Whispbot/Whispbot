using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLC.Commands
{
    public abstract class ERLCCommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract List<string> Aliases { get; }
        public abstract List<RateLimit> Ratelimits { get; }
        public abstract List<string> Usage { get; }
        public abstract Task ExecuteAsync(ERLCCommandContext ctx);
    }

    public class RateLimit
    {
        public int amount;
        public TimeSpan per;
        public RateLimitType type;
    }

    public enum RateLimitType
    {
        Global,
        Guild,
        User
    }

    public class ERLCCommandContext(Client client, Message message, ERLCServerConfig server, string username, string userId, List<string> args, List<string> flags)
    {
        public Client client = client;
        public Message message = message;
        public ERLCServerConfig server = server;
        public List<string> args = args;
        public List<string> flags = flags;

        public string robloxUsername = username;
        public string robloxUserId = userId;

        public string? GuildId => message.channel?.guild_id;
        public Guild? Guild => GuildId is not null ? DiscordCache.Guilds.Get(GuildId).Result : null;
        public User? User => UserId is not null ? DiscordCache.Users.Get(UserId).Result : null;
        public string? UserId => UserConfig?.id.ToString();

        private UserConfig? _userConfig = null;
        public UserConfig? UserConfig
        {
            get
            {
                if (_userConfig is not null) return _userConfig;

                _userConfig = Postgres.SelectFirst<UserConfig>("SELECT * FROM user_config WHERE roblox_id = @1", [long.Parse(robloxUserId)]);
                return _userConfig;
            }
        }
        public GuildConfig? GuildConfig => GuildId is not null ? WhispCache.GuildConfig.Get(GuildId).Result : null;

        public Strings.Language Language => (Strings.Language)(UserConfig?.language ?? GuildConfig?.default_language ?? 0);

        public async Task<Tools.ERLC.PRC_Response?> Reply(string content)
        {
            return await Tools.ERLC.SendCommand(server, $":pm {robloxUsername} {content.Process(Language)}");
        }
    }
}
