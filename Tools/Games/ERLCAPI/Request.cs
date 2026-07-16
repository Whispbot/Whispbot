using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Tools.Games.ERLCAPI.Classes;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCRequest
    {
        public static async Task<PRCResponse?> Request(string method, string url, string? serverId, string? encryptedApiKey, object? body = null)
        {
            return await ERLCQueue.Queue.Enqueue<PRCResponse>(new PRCRequest
            {
                method = method,
                endpoint = url,
                serverId = serverId,
                apiKey = encryptedApiKey,
                body = body
            });
        }

        public static T? ConvertResponseTo<T>(PRCResponse response) where T : class
        {
            var data = response.data;
            if (data is null) return null;

            if (data is T t) return t;

            if (data is JToken token)
            {
                try
                {
                    return token.ToObject<T>();
                }
                catch
                {
                    return null;
                }
            }

            if (data is string s)
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(s);
                }
                catch
                {
                    return null;
                }
            }

            try
            {
                var serialized = JsonConvert.SerializeObject(data);
                return JsonConvert.DeserializeObject<T>(serialized);
            }
            catch
            {
                return null;
            }
        }
    }
}
