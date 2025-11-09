using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands
{
    public abstract class Command
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract List<string> Aliases { get; }
        public abstract Module Module { get; }
        public abstract bool GuildOnly { get; }
        public abstract List<RateLimit> Ratelimits { get; }
        public abstract List<string> Usage { get; }
        public abstract Task ExecuteAsync(CommandContext ctx);
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

    [Flags]
    public enum Module
    {
        General = 1 << 31,
        Staff = 1 << 30,
        Shifts = 1 << 0,
        RobloxModeration = 1 << 1,
        ERLC = 1 << 2,
        DiscordModeration = 1 << 3
    }

    public class CommandContext (Client client, Message message, List<string> args, List<string> flags)
    {
        public Client client = client;
        public Message message = message;
        public List<string> args = args;
        public List<string> flags = flags;

        public string? GuildId => message.channel?.guild_id;
        public Guild? Guild => GuildId is not null ? DiscordCache.Guilds.Get(GuildId).WaitFor() : null;
        public User? User => message.author;
        public string? UserId => User?.id;

        public Message? repliedMessage = null;

        public UserConfig? UserConfig => UserId is not null ? WhispCache.UserConfig.Get(UserId).WaitFor() : null;
        public GuildConfig? GuildConfig => GuildId is not null ? WhispCache.GuildConfig.Get(GuildId).WaitFor() : null;

        public Tools.Strings.Language Language => (Tools.Strings.Language)(UserConfig?.language ?? GuildConfig?.default_language ?? 0);


        public async Task<(Message?, DiscordError?)> Reply(MessageBuilder content)
        {
            if (message.channel is null) return (null, new(new()));

            (Message? sentMessage, DiscordError? error) = await message.channel.Send(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });

            if (sentMessage is not null) repliedMessage = sentMessage;

            return (sentMessage, error);
        }

        public async Task<(Message?, DiscordError?)> Reply(string content)
        {
            return await Reply(new MessageBuilder { content = content });
        }

        public async Task<(Message?, DiscordError?)> EditResponse(MessageBuilder content)
        {
            if (repliedMessage is not null)
            {
                return await repliedMessage.Edit(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
            }
            else return await Reply(content);
        }

        public async Task<(Message?, DiscordError?)> EditResponse(string content)
        {
            return await EditResponse(new MessageBuilder { content = content });
        }
    }
}
