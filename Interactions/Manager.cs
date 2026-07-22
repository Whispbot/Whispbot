using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Interactions.Roblox_Connection;
using Whispbot.Interactions.Roblox_Moderations;
using Whispbot.Interactions.Shifts;
using Whispbot.Languages;
using Whispbot.Tools.Logger;

namespace Whispbot.Interactions
{
    public static class InteractionManager
    {
        private static readonly List<InteractionCommandData> _interactions = [];

        public static void Init(DiscordShardedClient client)
        {
            RegisterInteraction(new RobloxDisconnect());
            RegisterInteraction(new RobloxDisconnect());

            // Shifts
            RegisterInteraction(new Clockin());
            RegisterInteraction(new Clockout());

            // Shifts Admin
            RegisterInteraction(new AdminClockin());
            RegisterInteraction(new AdminClockout());
            RegisterInteraction(new AdminList());
            RegisterInteraction(new AdminBack());
            RegisterInteraction(new AdminModify());
            RegisterInteraction(new AdminModifyModal());
            RegisterInteraction(new AdminModifyShift());
            RegisterInteraction(new AdminDeleteShift());
            RegisterInteraction(new AdminDeleteShiftConfirm());
            RegisterInteraction(new AdminChangeType());
            RegisterInteraction(new AdminChangeTypeModal());
            RegisterInteraction(new AdminAddTime());
            RegisterInteraction(new AdminAddTimeModal());
            RegisterInteraction(new AdminRemoveTime());
            RegisterInteraction(new AdminRemoveTimeModal());
            RegisterInteraction(new AdminSetTime());
            RegisterInteraction(new AdminSetTimeModal());
            RegisterInteraction(new AdminWipeShifts());
            RegisterInteraction(new AdminWipeShiftsConfirm());
            RegisterInteraction(new ShiftLeaderboard());

            // Roblox Moderation
            RegisterInteraction(new RobloxEditType());
            RegisterInteraction(new EditTypeButton());
            RegisterInteraction(new EditTypeModal());
            RegisterInteraction(new EditReasonButton());
            RegisterInteraction(new EditReasonModal());
            RegisterInteraction(new DeleteButton());
            RegisterInteraction(new DeleteConfirm());

            RegisterInteraction(new BanRequestApprove());
            RegisterInteraction(new BanRequestApproveModal());
            RegisterInteraction(new BanRequestDeny());

            Logging.Log($"Loaded {_interactions.Count} interactions");

            client.InteractionCreated += async (interaction) =>
            {
                await HandleInteraction(client, interaction);
            };
        }

        public static void RegisterInteraction(InteractionCommandData interaction)
        {
            if (_interactions.Any(i => i.CustomId == interaction.CustomId && i.Type == interaction.Type)) return;
            _interactions.Add(interaction);
        }

        public static async Task HandleInteraction(DiscordShardedClient client, SocketInteraction interaction)
        {
            if (interaction.Type == InteractionType.Ping) return;
            else if (interaction.Type == InteractionType.ApplicationCommandAutocomplete) await Autocomplete.Handle(interaction);
            else if (interaction.Type == InteractionType.ApplicationCommand) await Commands.Handle(client, interaction);

            if (interaction.Data is not IComponentInteractionData data) return;

            List<string> args = [.. data.CustomId.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            if (args.Count == 0) return;
            string command = args[0];
            args.RemoveAt(0);

            InteractionCommandData? commandData = _interactions.FirstOrDefault(i => i.CustomId == command && i.Type == interaction.Type);
            if (commandData is null) return;

            var ctx = new InteractionContext(client, interaction, args);

            var localeMatches = Translator.LanguageInfo.Where(l => l.Value.Item1 == interaction.UserLocale);
            var language = (localeMatches.Any() ? localeMatches.First().Key : 0);
            if (ctx.UserConfig is not null && (ctx.UserConfig?.language ?? ctx.GuildConfig?.default_language) != language)
            {
                ctx.UserConfig!.language = language;
                _ = Task.Run(() => Postgres.Execute("UPDATE user_config SET language = @1 WHERE id = @2;", [language, ctx.UserId]));
            }

            await commandData.ExecuteAsync(ctx);
        }
    }
}
