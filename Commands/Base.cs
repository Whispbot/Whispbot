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
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace Whispbot.Commands
{
    public abstract class Command
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract List<string>? SlashCommand { get; }
        public abstract List<SlashCommandArg>? Arguments { get; }
        public abstract List<string> Schema { get; }
        public abstract List<string> Aliases { get; }
        public abstract Module Module { get; }
        public abstract bool GuildOnly { get; }
        public abstract List<RateLimit> Ratelimits { get; }
        public abstract List<string> Usage { get; }
        public abstract Task ExecuteAsync(CommandContext ctx);
    }

    public class SlashCommandArg(string name, string description, CommandArgType type, bool optional = false)
    {
        public string name = name;
        public string description = description;
        public bool optional = optional;
        public CommandArgType type = type;
        public int? min_length = null;
        public int? max_length = null;
        public int? min_values = null;
        public int? max_values = null;
    }

    public enum CommandArgType
    {
        String = 0,
        User = 1,
        Channel = 2,
        Role = 3,
        Duration = 4,
        Mentionable = 5,
        ShiftType = 6,
        RobloxType = 7,
        Case = 8,
        RobloxUser = 9,
        ERLCServer = 10,
        RobloxCase = 11,
        ERLCCommand = 12,
        DurationString = 13
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
        DiscordModeration = 1 << 3,
        Tickets = 1 << 4,
    }

    public enum CommandType
    {
        Legacy = 0,
        Slash = 1
    }

    public class CommandContext
    {
        public CommandContext(Client client, Message message, CommandArguments args)
        {
            this.client = client;
            this.message = message;
            this.args = args;
            this.type = CommandType.Legacy;
        }

        public CommandContext(Client client, Interaction interaction, CommandArguments args)
        {
            this.client = client;
            this.interaction = interaction;
            this.args = args;
            this.type = CommandType.Slash;
        }

        public Client client;
        public CommandType type;
        public Message? message;
        public Interaction? interaction;
        public CommandArguments args;

        public string? GuildId => 
            type == CommandType.Legacy ? message?.channel?.guild_id : interaction?.guild_id;
        public Guild? Guild => GuildId is not null ? DiscordCache.Guilds.Get(GuildId).GetAwaiter().GetResult() : null;
        public User? User => 
            type == CommandType.Legacy ? message?.author : interaction?.member?.user;
        public string? UserId => User?.id;

        public Message? repliedMessage = null;

        public UserConfig? UserConfig => UserId is not null ? WhispCache.UserConfig.Get(UserId).WaitFor() : null;
        public GuildConfig? GuildConfig => GuildId is not null ? WhispCache.GuildConfig.Get(GuildId).WaitFor() : null;

        public Tools.Strings.Language Language => (Tools.Strings.Language)(UserConfig?.language ?? GuildConfig?.default_language ?? 0);

        private MessageBuilder Process(MessageBuilder message)
        {
            return JsonConvert.DeserializeObject<MessageBuilder>(JsonConvert.SerializeObject(message).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." };
        }

        public async Task<(Message?, DiscordError?)> Reply(MessageBuilder content, bool ephemeral = false)
        {
            using var _ = Tracer.Start("Reply");

            if (type == CommandType.Legacy)
            {
                if (message?.channel is null) return (null, new(new()));

                (Message? sentMessage, DiscordError? error) = await message.channel.Send(Process(content));

                if (sentMessage is not null) repliedMessage = sentMessage;

                return (sentMessage, error);
            }
            else
            {
                if (interaction is null) return (null, new(new()));

                if (ephemeral) content.flags |= MessageFlags.Ephemeral;

                await interaction.Respond(Process(content));

                return (null, null);
            }
        }

        public async Task<(Message?, DiscordError?)> Reply(string content, bool ephemeral = false)
        {
            return await Reply(new MessageBuilder { content = content }, ephemeral);
        }

        public async Task<(Message?, DiscordError?)> EditResponse(MessageBuilder content)
        {
            using var _ = Tracer.Start($"EditReply");

            if (type == CommandType.Legacy)
            {
                if (repliedMessage is not null)
                {
                    return await repliedMessage.Edit(Process(content));
                }
                else return await Reply(content);
            }
            else
            {
                if (interaction is null) return (null, new(new()));

                await interaction.EditResponse(Process(content));

                return (null, null);
            }
        }

        public async Task<(Message?, DiscordError?)> EditResponse(string content)
        {
            return await EditResponse(new MessageBuilder { content = content });
        }
    }
}

