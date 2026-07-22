using Discord;
using Discord.WebSocket;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Interactions;
using Whispbot.Languages;

namespace Whispbot.Tools
{
    public class WhispPermissions
    {
        public static readonly Collection<ulong, List<PermissionRole>> permissionRoles = new(async (key) =>
        {
            return Postgres.Select<PermissionRole>(@"SELECT * FROM permission_roles WHERE guild_id = @1", [key]);
        });

        public static bool HasPermissionsAny(BotPermissions userPermissions, BotPermissions requiredPermissions)
        {
            return (userPermissions & requiredPermissions) != 0;
        }
        public static bool HasPermissionsAll(BotPermissions userPermissions, BotPermissions requiredPermissions)
        {
            return (userPermissions & requiredPermissions) == requiredPermissions;
        }

        public async static Task<bool> HasPermission(ulong guildId, ulong userId, BotPermissions requiredPermissions)
        {
            BotPermissions permissions = await GetPermissions(guildId, userId);
            return HasPermissionsAny(permissions, requiredPermissions | BotPermissions.Administrator);
        }
        public async static Task<bool> HasAllPermissions(ulong guildId, ulong userId, BotPermissions requiredPermissions)
        {
            BotPermissions permissions = await GetPermissions(guildId, userId);
            return HasPermissionsAll(permissions, requiredPermissions);
        }

        public async static Task<BotPermissions> GetPermissions(ulong guildId, ulong userId)
        {
            List<PermissionRole>? pRoles = await permissionRoles.Get(guildId);
            if (pRoles is null || pRoles.Count == 0) return 0;

            SocketGuild? guild = Config.client?.GetGuild(guildId);
            if (guild is null) return 0;

            IGuildUser? member = guild.GetUser(userId);
            if (member is null) return 0;

            BotPermissions permissions = 0;

            foreach (var role in pRoles.Where(r => r.roles.Any(ro => member.RoleIds.Contains(ro)))) permissions |= (BotPermissions)role.permissions;

            return permissions;
        }

        public static async Task<bool> CheckPermissionsMessage(CommandContext ctx, BotPermissions permissions)
        {
            BotPermissions userPermissions = await GetPermissions(ctx.GuildId, ctx.UserId);
            if (!HasPermissionsAny(userPermissions, permissions | BotPermissions.Administrator))
            {
                List<string> missingPermissions = [];
                foreach (BotPermissions perm in Enum.GetValues<BotPermissions>())
                {
                    if ((permissions & perm) != 0 && (userPermissions & perm) == 0) missingPermissions.Add(perm.ToString());
                }

                await ctx.Reply(
                    embed: new EmbedBuilder()
                        .WithTitle("{string.title.permissions.invalid}")
                        .WithDescription("{string.content.permissions.invalid}.".Translate(ctx.Language, missingPermissions.Join(", ", " or ")))
                        .WithColor(new Color(150, 50, 50))
                        .Build()
                );

                return false;
            }

            return true;
        }

        public static async Task<bool> CheckPermissionsInteraction(InteractionContext ctx, BotPermissions permissions)
        {
            if (ctx.GuildId is null) return false;

            BotPermissions userPermissions = await GetPermissions(ctx.GuildId.Value, ctx.UserId);
            if (!HasPermissionsAny(userPermissions, permissions | BotPermissions.Administrator))
            {
                List<string> missingPermissions = [];
                foreach (BotPermissions perm in Enum.GetValues<BotPermissions>())
                {
                    if ((permissions & perm) != 0 && (userPermissions & perm) == 0) missingPermissions.Add(perm.ToString());
                }

                await ctx.Respond(
                    embed: new EmbedBuilder()
                        .WithTitle("{string.title.permissions.invalid}")
                        .WithDescription("{string.content.permissions.invalid}.".Translate(ctx.Language, missingPermissions.Join(", ", " or ")))
                        .WithColor(new Color(150, 50, 50))
                        .Build(),
                    ephemeral: true
                );

                return false;
            }

            return true;
        }

        public static async Task<(bool, List<string>)> CheckModule(ulong guildId, Module modules)
        {
            GuildConfig? config = await WhispCache.GuildConfig.Get(guildId);
            if (config is null)
            {
                return (false, []);
            }
            Module enabledModules = (Module)config.enabled_modules;
            if ((enabledModules & modules) == 0)
            {
                List<string> missingModules = [];
                foreach (Module module in Enum.GetValues<Module>())
                {
                    if ((modules & module) != 0 && (enabledModules & module) == 0) missingModules.Add(module.ToString());
                }
                return (false, missingModules);
            }
            return (true, []);
        }

        public static async Task<bool> CheckModuleMessage(CommandContext ctx, Module modules)
        {
            var (enabled, missingModules) = await CheckModule(ctx.GuildId, modules);

            if (!enabled)
            {
                await ctx.Reply(
                    embed: new EmbedBuilder()
                        .WithTitle("{string.title.module.disabled}")
                        .WithDescription("{string.content.module.disabled}.".Translate(ctx.Language, missingModules.Join(", ", " or ")))
                        .WithColor(new Color(150, 50, 50))
                        .Build()
                );
            }

            return enabled;
        }
    }

    public class PermissionRole
    {
        public Guid id;
        public long guild_id;
        public string name = "";
        public long permissions;
        public List<ulong> roles = [];
        public DateTimeOffset created_at;
        public DateTimeOffset updated_at;
    }

    [Flags]
    public enum BotPermissions
    {
        Administrator = 1 << 0,
        ConfigureGuild = 1 << 1,
        UseShifts = 1 << 2,
        ManageShifts = 1 << 3,
        UseERLC = 1 << 4,
        ERLCOWner = 1 << 5,
        ERLCAdmin = 1 << 6,
        ERLCModerator = 1 << 7,
        UseRobloxModerations = 1 << 8,
        ManageRobloxModerations = 1 << 9,
        UseBanRequests = 1 << 10,
        ManageBanRequests = 1 << 11,
    }
}
