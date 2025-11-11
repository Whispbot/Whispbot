using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Interactions;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Tools
{
    public class WhispPermissions
    {
        public static readonly Collection<List<PermissionRole>> permissionRoles = new(async (key, args) =>
        {
            return Postgres.Select<PermissionRole>(@"SELECT * FROM permission_roles WHERE guild_id = @1", [long.Parse(key)]);
        });

        public static bool HasPermissionsAny(BotPermissions userPermissions, BotPermissions requiredPermissions)
        {
            return (userPermissions & requiredPermissions) != 0;
        }
        public static bool HasPermissionsAll(BotPermissions userPermissions, BotPermissions requiredPermissions)
        {
            return (userPermissions & requiredPermissions) == requiredPermissions;
        }

        public async static Task<bool> HasPermission(string guildId, string userId, BotPermissions requiredPermissions)
        {
            BotPermissions permissions = await GetPermissions(guildId, userId);
            return HasPermissionsAny(permissions, requiredPermissions | BotPermissions.Administrator);
        }
        public async static Task<bool> HasAllPermissions(string guildId, string userId, BotPermissions requiredPermissions)
        {
            BotPermissions permissions = await GetPermissions(guildId, userId);
            return HasPermissionsAll(permissions, requiredPermissions);
        }

        public async static Task<BotPermissions> GetPermissions(string guildId, string userId)
        {
            List<PermissionRole>? pRoles = await permissionRoles.Get(guildId);
            if (pRoles is null || pRoles.Count == 0) return 0;

            Guild? guild = await DiscordCache.Guilds.Get(guildId);
            if (guild is null) return 0;

            Member? member = await guild.members.Get(userId);
            if (member is null) return 0;

            BotPermissions permissions = 0;

            foreach (var role in pRoles.Where(r => r.roles.Any(ro => member.roles?.Contains(ro) ?? false))) permissions |= (BotPermissions)role.permissions;

            return permissions;
        }


        public static async Task<bool> CheckPermissionsMessage(CommandContext ctx, BotPermissions permissions)
        {
            if (ctx.GuildId is null || ctx.UserId is null) return false;

            BotPermissions userPermissions = await GetPermissions(ctx.GuildId, ctx.UserId);
            if (!HasPermissionsAny(userPermissions, permissions | BotPermissions.Administrator))
            {
                List<string> missingPermissions = [];
                foreach (BotPermissions perm in Enum.GetValues(typeof(BotPermissions)))
                {
                    if ((permissions & perm) != 0 && (userPermissions & perm) == 0) missingPermissions.Add(perm.ToString());
                }

                await ctx.Reply(new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            title = "{string.title.permissions.invalid}",
                            description = "{string.content.permissions.invalid}.".Process(ctx.Language, new Dictionary<string, string>() {
                                { "missing_perms", missingPermissions.Join(", ", " or ") }
                            }),
                            color = new Color(150, 50, 50).ToInt()
                        }
                    ]
                });

                return false;
            }

            return true;
        }

        public static async Task<bool> CheckPermissionsInteraction(InteractionContext ctx, BotPermissions permissions)
        {
            if (ctx.GuildId is null || ctx.UserId is null) return false;

            BotPermissions userPermissions = await GetPermissions(ctx.GuildId, ctx.UserId);
            if (!HasPermissionsAny(userPermissions, permissions | BotPermissions.Administrator))
            {
                List<string> missingPermissions = [];
                foreach (BotPermissions perm in Enum.GetValues(typeof(BotPermissions)))
                {
                    if ((permissions & perm) != 0 && (userPermissions & perm) == 0) missingPermissions.Add(perm.ToString());
                }

                await ctx.Respond(new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            title = "{string.title.permissions.invalid}",
                            description = "{string.content.permissions.invalid}.".Process(ctx.Language, new Dictionary<string, string>() {
                                { "missing_perms", missingPermissions.Join(", ", " or ") }
                            }),
                            color = new Color(150, 50, 50).ToInt()
                        }
                    ]
                }, true);

                return false;
            }

            return true;
        }
        
        public static async Task<bool> CheckModuleMessage(CommandContext ctx, Module modules)
        {
            if (ctx.GuildId is null) return false;

            GuildConfig? config = await WhispCache.GuildConfig.Get(ctx.GuildId);
            if (config is null)
            {
                await ctx.Reply("{emoji.warning} {emoji.errors.dbfailed}");
                return false;
            }

            Module enabledModules = (Module)config.enabled_modules;

            if ((enabledModules & modules) == 0)
            {
                List<string> missingModules = [];
                foreach (Module module in Enum.GetValues(typeof(Module)))
                {
                    if ((modules & module) != 0 && (enabledModules & module) == 0) missingModules.Add(module.ToString());
                }

                await ctx.Reply(new MessageBuilder()
                {
                    embeds = [
                        new EmbedBuilder()
                        {
                            title = "{string.title.module.disabled}",
                            description = "{string.content.module.disabled}.".Process(ctx.Language, new Dictionary<string, string>() {
                                { "missing_modules", missingModules.Join(", ", " or ") }
                            }),
                            color = new Color(150, 50, 50).ToInt()
                        }
                    ]
                });

                return false;
            }

            return true;
        }
    }

    public class PermissionRole
    {
        public Guid id;
        public long guild_id;
        public string name = "";
        public long permissions;
        public List<string> roles = [];
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
        ERLCModerator = 1 << 7
    }
}
