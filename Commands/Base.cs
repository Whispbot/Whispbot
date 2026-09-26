using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Logging;
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
        public CommandContext(DiscordShardedClient client, SocketMessage message, CommandArguments args)
        {
            this.client = client;
            this.message = message;
            this.args = args;
            this.type = CommandType.Legacy;
        }

        public CommandContext(DiscordShardedClient client, SocketInteraction interaction, CommandArguments args)
        {
            this.client = client;
            this.interaction = interaction;
            this.args = args;
            this.type = CommandType.Slash;
        }

        public DiscordShardedClient client;
        public CommandType type;
        public SocketMessage? message;
        public SocketInteraction? interaction;
        public CommandArguments args;

        public SocketGuildChannel GuildChannel =>
            ((message?.Channel ?? interaction?.Channel) as SocketGuildChannel) ?? throw new InvalidOperationException("Channel not from guild");
        public ulong GuildId => 
            GuildChannel.Guild.Id;
        public SocketGuild Guild => 
            client.Guilds.SingleOrDefault(g => g.Id == GuildId) ?? throw new InvalidOperationException("Guild not found");
        public SocketUser User =>
            message?.Author ?? interaction?.User ?? throw new InvalidOperationException("User not found");
        public SocketGuildUser Member => Guild.GetUser(UserId);
        public ulong UserId => User.Id;

        public bool hasResponded = false;

        public RestUserMessage? repliedMessage = null;

        public UserConfig? UserConfig => WhispCache.UserConfig.Get(UserId).Result;
        public GuildConfig? GuildConfig => WhispCache.GuildConfig.Get(GuildId).Result;

        public Language Language => UserConfig?.language ?? GuildConfig?.default_language ?? 0;

        public string String(string name, params string[] args)
        {
            // CHANGE THIS
            return Translator.Get(Language, name, args);
        }
        public IEmote Emoji(string name)
        {
            return Emojis.Get(name);
        }

        public async Task Reply(
            string? text = null,
            bool ephemeral = false,
            bool isTTS = false,
            Embed? embed = null,
            RequestOptions? options = null,
            AllowedMentions? allowedMentions = null,
            MessageReference? messageReference = null,
            MessageComponent? components = null,
            ISticker[]? stickers = null,
            Embed[]? embeds = null,
            MessageFlags flags = MessageFlags.None,
            PollProperties? poll = null
        )
        {
            using var _ = Tracer.Start("Reply");

            if (type == CommandType.Legacy)
            {
                var sentMessage = await message!.Channel.SendMessageAsync(
                    text,
                    isTTS,
                    embed,
                    options,
                    allowedMentions,
                    messageReference,
                    components,
                    stickers,
                    embeds,
                    flags,
                    poll
                );

                if (sentMessage is not null) repliedMessage = sentMessage;
            }
            else
            {
                if (interaction!.HasResponded)
                {
                    await interaction.ModifyOriginalResponseAsync(m =>
                    {
                        m.Content = text;
                        m.Embeds = embeds;
                        m.Embed = embed;
                        m.AllowedMentions = allowedMentions;
                        m.Components = components;
                        m.Flags = flags;
                    });
                }
                else
                {
                    await interaction.RespondAsync(
                        text,
                        embeds,
                        isTTS,
                        ephemeral,
                        allowedMentions,
                        components,
                        embed,
                        options,
                        poll,
                        flags
                    );
                }
            }

            hasResponded = true;
        }

        public async Task EditResponse(Action<MessageProperties> func)
        {
            using var _ = Tracer.Start($"EditReply");

            if (type == CommandType.Legacy)
            {
                if (repliedMessage is null) return;
                await repliedMessage.ModifyAsync(func);
            }
            else
            {
                await interaction!.ModifyOriginalResponseAsync(func);
            }
        }

        public async Task EditResponse(
            string? text = null,
            bool ephemeral = false,
            bool isTTS = false,
            Embed? embed = null,
            RequestOptions? options = null,
            AllowedMentions? allowedMentions = null,
            MessageReference? messageReference = null,
            MessageComponent? components = null,
            ISticker[]? stickers = null,
            Embed[]? embeds = null,
            MessageFlags flags = MessageFlags.None,
            PollProperties? poll = null
        )
        {
            if (hasResponded)
            {
                await EditResponse(m =>
                {
                    if (text is not null ) m.Content = text;
                    if (embed is not null) m.Embed = embed;
                    if (embeds is not null) m.Embeds = embeds;
                    if (allowedMentions is not null) m.AllowedMentions = allowedMentions;
                    if (components is not null) m.Components = components;
                    if (flags != MessageFlags.None) m.Flags = m.Flags.GetValueOrDefault(MessageFlags.None) | flags;
                });
            }
            else
            {
                await Reply(text, isTTS, ephemeral, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags, poll);
            }
        }
    }
}

