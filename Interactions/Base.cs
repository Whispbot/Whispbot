using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Extensions;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions
{
    public abstract class InteractionData
    {
        public abstract string CustomId { get; }
        public abstract InteractionType Type { get; }
        public abstract Task ExecuteAsync(InteractionContext ctx);
    }

    public class InteractionContext(Client client, Interaction interaction, List<string> args)
    {
        public Client client = client;
        public Interaction interaction = interaction;
        public List<string> args = args;

        public string? GuildId => interaction.guild_id;
        public string? ChannelId => interaction.channel?.id;
        public string? UserId => interaction.user?.id ?? interaction.member?.user?.id;
        public Guild? Guild => GuildId is not null ? DiscordCache.Guilds.Get(GuildId).WaitFor() : null;
        public Channel? Channel => ChannelId is not null ? DiscordCache.Channels.Get(ChannelId).WaitFor() : null;
        public User? User => interaction.user ?? interaction.member?.user;

        public UserConfig? UserConfig => UserId is not null ? WhispCache.UserConfig.Get(UserId).WaitFor() : null;
        public GuildConfig? GuildConfig => GuildId is not null ? WhispCache.GuildConfig.Get(GuildId).WaitFor() : null;

        public Tools.Strings.Language Language => (Tools.Strings.Language)(UserConfig?.language ?? GuildConfig?.default_language ?? 0);

        public async Task Respond(MessageBuilder content, bool ephmeral = false)
        {
            if (ephmeral && !content.flags.HasFlag(MessageFlags.Ephemeral)) content.flags |= MessageFlags.Ephemeral;
            await interaction.Respond(JsonConvert.DeserializeObject<MessageBuilder>(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
        }

        public async Task Respond(string content, bool ephmeral = false)
        {
            await Respond(new MessageBuilder { content = content }, ephmeral);
        }

        public async Task<(Message?, DiscordError?)> UpdateResponse(MessageBuilder content)
        {
            return await interaction.EditResponse(JsonConvert.DeserializeObject<MessageBuilder>(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
        }

        public async Task<(Message?, DiscordError?)> UpdateResponse(string content)
        {
            return await UpdateResponse(new MessageBuilder { content = content });
        }

        public async Task DeferResponse(bool ephmeral = false)
        {
            await interaction.DeferResponse(ephmeral);
        }

        public async Task DeferUpdate()
        {
            await interaction.DeferUpdate();
        }

        public async Task DeleteResponse()
        {
            await interaction.DeleteResponse();
        }

        public async Task<(Message?, DiscordError?)> SendFollowup(MessageBuilder content, bool ephemeral = false)
        {
            if (ephemeral && !content.flags.HasFlag(MessageFlags.Ephemeral)) content.flags |= MessageFlags.Ephemeral;
            return await interaction.SendFollowup(JsonConvert.DeserializeObject<MessageBuilder>(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
        }

        public async Task<(Message?, DiscordError?)> SendFollowup(string content, bool ephemeral = false)
        {
            return await SendFollowup(new MessageBuilder { content = content }, ephemeral);
        }

        public async Task<(Message?, DiscordError?)> EditFollowup(string messageId, MessageBuilder content)
        {
            return await interaction.EditFollowup(messageId, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
        }

        public async Task<(Message?, DiscordError?)> EditMessage(MessageBuilder content)
        {
            if (interaction.message is null) return (null, new DiscordError(new()));
            return await interaction.message.Edit(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(content).Process(Language)) ?? new MessageBuilder() { content = "Something went wrong..." });
        }

        public async Task<(Message?, DiscordError?)> EditFollowup(string messageId, string content)
        {
            return await EditFollowup(messageId, new MessageBuilder { content = content });
        }

        public async Task DeleteFollowup(string messageId)
        {
            await interaction.DeleteFollowup(messageId);
        }

        public async Task AutocompleteResult(List<AutocompleteChoices> choices)
        {
            await interaction.AutocompleteResult(choices);
        }

        public async Task ShowModal(ModalBuilder modal)
        {
            modal = JsonConvert.DeserializeObject<ModalBuilder>(JsonConvert.SerializeObject(modal).Process(Language)) ?? modal;
            await interaction.ShowModal(modal);
        }

        public async Task LaunchActivity()
        {
            await interaction.LaunchActivity();
        }

        public async Task<bool> CheckAllowed(string allowedUserId)
        {
            if (allowedUserId != UserId)
            {
                await Respond("{emoji.cross} {string.errors.notyours}.", true);
                return true;
            }
            else return false;
        }
        public async Task<bool> CheckAllowed()
        {
            return await CheckAllowed(args.FirstOrDefault() ?? "");
        }
    }
}
