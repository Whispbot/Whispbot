using Amazon.S3.Model;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Extensions;
using Whispbot.Languages;

namespace Whispbot.Interactions
{
    public abstract class InteractionCommandData
    {
        public abstract string CustomId { get; }
        public abstract InteractionType Type { get; }
        public abstract Task ExecuteAsync(InteractionContext ctx);
    }

    public class InteractionContext(DiscordShardedClient client, SocketInteraction interaction, List<string> args)
    {
        public DiscordShardedClient client = client;
        public SocketInteraction interaction = interaction;
        public List<string> args = args;

        public ulong? GuildId => interaction.GuildId;
        public ulong? ChannelId => interaction.ChannelId;
        public ulong UserId => interaction.User.Id;
        public SocketGuild? Guild => GuildId is not null ? client.GetGuild(GuildId.Value) : null;
        public SocketChannel? Channel => ChannelId is not null ? client.GetChannel(ChannelId.Value) : null;
        public SocketUser User => interaction.User;

        public UserConfig? UserConfig => WhispCache.UserConfig.Get(UserId).Result;
        public GuildConfig? GuildConfig => GuildId is not null ? WhispCache.GuildConfig.Get(GuildId.Value).Result : null;

        public Language Language => (Language)(UserConfig?.language ?? GuildConfig?.default_language ?? 0);

        public string String(string key, params string[] args)
        {
            return Translator.Get(Language, key, args);
        }

        public async Task Respond(
            string? text = null, 
            Embed[]? embeds = null, 
            bool isTTS = false, 
            bool ephemeral = true, 
            AllowedMentions? allowedMentions = null,
            MessageComponent? components = null,
            Embed? embed = null,
            RequestOptions? options = null,
            PollProperties? poll = null,
            MessageFlags flags = MessageFlags.None
        )
        {
            await interaction.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options, poll, flags);
        }

        public async Task UpdateResponse(Action<MessageProperties> func)
        {
            await interaction.ModifyOriginalResponseAsync(func);
        }

        public async Task DeferResponse(bool ephemeral = true)
        {
            await interaction.DeferAsync(ephemeral);
        }

        public async Task DeleteResponse()
        {
            await interaction.DeleteOriginalResponseAsync();
        }

        public async Task<RestFollowupMessage> SendFollowup(
            string? text = null,
            Embed[]? embeds = null,
            bool isTTS = false,
            bool ephemeral = false,
            AllowedMentions? allowedMentions = null,
            MessageComponent? components = null,
            Embed? embed = null,
            RequestOptions? options = null,
            PollProperties? poll = null,
            MessageFlags flags = MessageFlags.None
        )
        {
            return await interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options, poll, flags);
        }

        public async Task EditMessage(Action<MessageProperties> func)
        {
            var message = await interaction.GetOriginalResponseAsync();

            await message.ModifyAsync(func);
        }

        public async Task AutocompleteResult(IEnumerable<AutocompleteResult> choices)
        {
            if (interaction is SocketAutocompleteInteraction autocomplete)
            {
                await autocomplete.RespondAsync(choices);
            }
        }

        public async Task ShowModal(Modal modal)
        {
            await interaction.RespondWithModalAsync(modal);
        }

        public async Task<bool> CheckAllowed(ulong allowedUserId)
        {
            if (allowedUserId != UserId)
            {
                await Respond("{emoji.cross} {string.errors.notyours}.", ephemeral: true);
                return true;
            }
            else return false;
        }
        public async Task<bool> CheckAllowed()
        {
            return await CheckAllowed(ulong.Parse(args.FirstOrDefault() ?? "0"));
        }
    }
}
