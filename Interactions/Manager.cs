using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Interactions.Roblox;
using Whispbot.Interactions.Shifts;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using YellowMacaroni.Discord.Sharding;

namespace Whispbot.Interactions
{
    public class InteractionManager
    {
        private List<InteractionData> _interactions = [];

        public InteractionManager()
        {
            RegisterInteraction(new RobloxConnect());
            RegisterInteraction(new RobloxDisconnect());

            RegisterInteraction(new Clockin());
            RegisterInteraction(new Clockout());

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

            if (Config.IsDev) Log.Debug($"[Debug] Loaded {_interactions.Count} interactions");
        }

        public void RegisterInteraction(InteractionData interaction)
        {
            if (_interactions.Any(i => i.CustomId == interaction.CustomId && i.Type == interaction.Type)) return;
            _interactions.Add(interaction);
        }

        public void HandleInteraction(Client client, Interaction interaction)
        {
            if (interaction.type == InteractionType.ApplicationCommand || interaction.type == InteractionType.Ping) return; // Handled by command manager

            if (interaction.data?.custom_id is null) return;

            List<string> args = [.. interaction.data.custom_id.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            if (args.Count == 0) return;
            string command = args[0];
            args.RemoveAt(0);

            InteractionData? data = _interactions.FirstOrDefault(i => i.CustomId == command && i.Type == interaction.type);

            if (data is null) return;

            var ctx = new InteractionContext(client, interaction, args);

            var localeMatches = Tools.Strings.Languages.Where(l => l.Value.Item1 == interaction.locale);
            int language = (int)(localeMatches.Any() ? localeMatches.First().Key : 0);
            if (ctx.UserConfig is not null && (ctx.UserConfig?.language ?? ctx.GuildConfig?.default_language) != language)
            {
                ctx.UserConfig!.language = language;
                Task.Run(() => Postgres.Execute("UPDATE user_config SET language = @1 WHERE id = @2;", [language, long.Parse(ctx.UserId!)]));
            }

            data.ExecuteAsync(ctx);
        }

        public void Attach(Client client)
        {
            client.InteractionCreate += (c, interaction) =>
            {
                if (c is not Client client) return;
                HandleInteraction(client, interaction);
            };
        }

        public void Attach(ShardingManager manager)
        {
            foreach (Shard shard in manager.shards)
            {
                Attach(shard.client);
            }
        }
    }
}
