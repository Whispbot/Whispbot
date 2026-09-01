using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Tools.Games.ERLCAPI.Classes;
using Whispbot.Tools.Logging;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCAPI
    {
        public static async Task<PRCResponse?> GetERLCServer(ERLCServerConfig server, CommandContext? ctx = null)
        {
            using var _ = Tracer.Start($"ERLC.GetServerV2");

            var response = await ERLCCache.GetCache(new PRCRequest // Fake request to check if the server is cached
            {
                method = "GET",
                endpoint = "/v2/server?Players=true&Staff=true&JoinLogs=true&Queue=true&KillLogs=true&CommandLogs=true&ModCalls=true&EmergencyCalls=true&Vehicles=true",
                serverId = server.internal_id
            });

            if (response is null)
            {
                if (ctx is not null)
                {
                    await ctx.Reply(ctx.GenerateLoadingMessage());
                }

                response = await FetchERLCServer(server);

                if (response is null)
                {
                    if (ctx is not null)
                    {
                        await ctx.EditResponse(m => m.Content = $"{ctx.Emoji("cross")} {ctx.String("erlc.errors.api_error")}");
                    }
                    return null;
                }
            }

            if (ctx is not null && Errors.ResponseHasError(ctx, response, out var errorMessage))
            {
                await ctx.EditResponse(m => { m.Components = errorMessage!; m.Flags = MessageFlags.ComponentsV2; });
            }
            else if (!response.success)
            {
                return null;
            }

            return response;
        }

        public static async Task<PRCResponse?> GetERLCServer(CommandContext ctx, ERLCServerConfig server)
        {
            return await GetERLCServer(server, ctx);
        }

        public static async Task<PRCResponse?> FetchERLCServer(ERLCServerConfig server)
        {
            return await ERLCRequest.Request(
                "GET",
                "/v2/server?Players=true&Staff=true&JoinLogs=true&Queue=true&KillLogs=true&CommandLogs=true&ModCalls=true&EmergencyCalls=true&Vehicles=true",
                server.internal_id,
                server.api_key
            );
        }

        public static async Task<PRCResponse?> SendCommand(ERLCServerConfig server, string command)
        {
            return await ERLCRequest.Request(
                "POST",
                "/v2/server/command",
                server.internal_id,
                server.api_key,
                new { command }
            );
        }

        public static string GenerateLoadingMessage(this CommandContext ctx)
        {
            return $"{ctx.Emoji("loading")} {ctx.String("erlc.loading")}";
        }

        public static string GenerateErrorMessage(this PRCResponse? response, CommandContext ctx)
        {
            return $"{ctx.Emoji("cross")} [{response?.error ?? 0}] {ctx.String($"erlc.errors.api.{response?.error.ToString().ToLower() ?? "unknown"}")}.";
        }
    }
}
