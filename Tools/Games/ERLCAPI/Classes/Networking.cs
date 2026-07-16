using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLCAPI.Classes
{
    public class PRCRequest
    {
        public string? serverId = null;
        public string? apiKey = null;
        public string endpoint = null!;
        public string method = null!;
        public object? body = null;
    }

    public class PRCResponse
    {
        public string? serverId = null!;
        public bool success = false;
        public long cachedAtMs = -1;
        public ErrorCode error = ErrorCode.Nothing;
        public string? error_message = "Something went wrong...";
        public object? data = null!;

        public DateTimeOffset? CachedAt => cachedAtMs != -1 ? DateTimeOffset.FromUnixTimeMilliseconds(cachedAtMs) : null;

        public ERLCServer? Server => ERLCRequest.ConvertResponseTo<ERLCServer>(this);
        public ERLCError? Error => ERLCRequest.ConvertResponseTo<ERLCError>(this);
    }

    public class PRCError
    {
        public ErrorCode error;
        public string message = null!;
        [JsonProperty("learn-more-and-docs")]
        public string learn_more_and_docs = null!;
        [JsonProperty("api-dashboard")]
        public string api_dashboard = null!;
    }
}
