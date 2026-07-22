using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Commands;
using Whispbot.Tools.Games.ERLCAPI.Classes;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class Errors
    {
        private static string? _appId = Environment.GetEnvironmentVariable("PRC_CLIENT_ID");

        public static bool ResponseHasError(CommandContext ctx, PRCResponse response, out MessageComponent? errorMessage)
        {
            if (response.success)
            {
                errorMessage = null;
                return false;
            }
            else
            {
                var builder = new ComponentBuilderV2()
                    .WithContainer(
                        new ContainerBuilder()
                            .WithTextDisplay($"## {ctx.String("erlc.errors.msg.title")}\n> {ctx.String($"erlc.errors.api.{response.error.ToString()?.ToLower() ?? "unknown"}")}")
                            .WithSeparator()
                            .WithTextDisplay($"{ctx.String("erlc.errors.msg.error")}\n```\n[{(int)response.error}] {response.Error?.message ?? response.error_message}{(response.Error?.docs is not null ? $"\n\n{response.Error.docs}" : "")}\n```")
                            .WithAccentColor(new Color(150, 0, 0))
                    );

                if (response.error == ErrorCode.NotAuthorized && response.serverId is not null && _appId is not null)
                {
                    builder.WithActionRow(
                        new ActionRowBuilder()
                            .WithButton(ctx.String("erlc.errors.msg.button"), style: ButtonStyle.Link, url: $"https://api.erlc.gg/server-owners/server/{response.serverId}/authorize/{_appId}")
                    );
                }

                errorMessage = builder.Build();
                
                return true;
            }
        }

        public static string? GetErrorMessage(ErrorCode code)
        {
            return code switch
            {
                ErrorCode.Nothing => null,

                // 0XXX, 1XXX
                ErrorCode.Unknown => "Unknown error occurred. If this persists, contact us via https://whisp.bot/support.",

                ErrorCode.RobloxServerError => "An error occurred communicating with Roblox or the in-game private server.",
                ErrorCode.InternalServerError => "An internal system error occurred.",

                // 2XXX
                ErrorCode.NoServerKeyProvided => "You did not provide a server-key.",
                ErrorCode.ServerKeyMalformed => "You provided an incorrectly formatted server-key.",
                ErrorCode.ServerKeyInvalid => "You provided an invalid or expired server-key.",
                ErrorCode.GlobalKeyInvalid => "You provided an invalid global API key.",
                ErrorCode.ServerKeyBanned => "Your server-key is currently banned from accessing the API.",

                // 3XXX
                ErrorCode.InvalidCommand => "You did not provide a valid command in the request body.",
                ErrorCode.ServerOffline => "The server you are attempting to reach is currently offline (has no players).",

                // 4XXX
                ErrorCode.NotAuthorized => "Whisp is not authorized to perform this action on this server.",
                ErrorCode.Ratelimited => "You are being ratelimited.",
                ErrorCode.CommandRestricted => "The command you are attempting to run is restricted.",
                ErrorCode.MessageProhibited => "The message you are trying to send is prohibited.",

                // 9XXX
                ErrorCode.ResourceRestricted => "The resource you are accessing is restricted.",
                ErrorCode.ServerOutOfDate => "The module running on the in-game server is out of date.",

                // 1XXXX
                ErrorCode.CircuitBreakerOpen => "It is likely that the PRC API is experiencing issues. Please try again later.",

                _ => null
            };
        }
    }

    /// <summary>
    /// Error codes returned by the PRC API along side their respective error messages (https://apidocs.erlc.gg/error-codes)
    /// </summary>
    public enum ErrorCode
    {
        Nothing = -1,
        // System Errors (0, 1001, 1002)
        /// <summary>
        /// Unknown error occurred. If this persists, contact PRC via an API ticket.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// An error occurred communicating with Roblox or the in-game private server.
        /// </summary>
        RobloxServerError = 1001,
        /// <summary>
        /// An internal system error occurred.
        /// </summary>
        InternalServerError = 1002,

        // Authentication Errors (2000 - 2004)
        /// <summary>
        /// You did not provide a server-key.
        /// </summary>
        NoServerKeyProvided = 2000,
        /// <summary>
        /// You provided an incorrectly formatted server-key.
        /// </summary>
        ServerKeyMalformed = 2001,
        /// <summary>
        /// You provided an invalid or expired server-key.
        /// </summary>
        ServerKeyInvalid = 2002,
        /// <summary>
        /// You provided an invalid global API key.
        /// </summary>
        GlobalKeyInvalid = 2003,
        /// <summary>
        /// Your server-key is currently banned from accessing the API.
        /// </summary>
        ServerKeyBanned = 2004,

        // Request Errors (3001 - 3002)
        /// <summary>
        /// You did not provide a valid command in the request body.
        /// </summary>
        InvalidCommand = 3001,
        /// <summary>
        /// The server you are attempting to reach is currently offline (has no players).
        /// </summary>
        ServerOffline = 3002,

        // Ratelimit and Access Errors (4000 - 4003)
        /// <summary>
        /// You are not authorized to perform this action on this server.
        /// </summary>
        NotAuthorized = 4000,
        /// <summary>
        /// You are being ratelimited.
        /// </summary>
        Ratelimited = 4001,
        /// <summary>
        /// The command you are attempting to run is restricted.
        /// </summary>
        CommandRestricted = 4002,
        /// <summary>
        /// The message you are trying to send is prohibited.
        /// </summary>
        MessageProhibited = 4003,

        // Special Codes (9998, 9999)
        /// <summary>
        /// The resource you are accessing is restricted.
        /// </summary>
        ResourceRestricted = 9998,
        /// <summary>
        /// The module running on the in-game server is out of date.
        /// </summary>
        ServerOutOfDate = 9999,

        // Our Codes (10000+)
        CircuitBreakerOpen = 10001
    }
}
