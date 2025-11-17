using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Databases
{
    public static class SentryConnection
    {
        public static void Init()
        {
            string? sentry_dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");

            if (sentry_dsn is null)
            {
                Log.Error("Could not connect to sentry, no SENTRY_DSN environment variable.");
                return;
            }

            try
            {
                SentrySdk.Init(options =>
                {
                    options.Dsn = sentry_dsn;

                    options.Debug = false;

                    options.AutoSessionTracking = true;
                });
                Log.Information("Initialized sentry");
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to initialize sentry, not fatal");
                Log.Warning(ex.Message);
            }
        }
    }

    public static class SentryResolver
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string? _sentryToken = Environment.GetEnvironmentVariable("SENTRY_TOKEN");
        private static readonly string? _sentryOrg = Environment.GetEnvironmentVariable("SENTRY_ORG");

        public static async Task<SentryResolvedEvent?> ResolveEventId(string id)
        {
            if (_sentryToken is null || _sentryOrg is null)
            {
                throw new Exception("SENTRY_TOKEN or SENTRY_ORG environment variable is not set.");
            }

            HttpRequestMessage request = new(HttpMethod.Get, $"https://sentry.io/api/0/organizations/{_sentryOrg}/eventids/{id}/");
            request.Headers.Add("Authorization", $"Bearer {_sentryToken}");

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<SentryResolvedEvent>(await response.Content.ReadAsStringAsync());
            }
            else return null;
        }

        public class SentryResolvedEvent
        {
            public string organizationSlug = "";
            public string projectSlug = "";
            public string groupId = "";
            public string eventId = "";
            [JsonProperty("event")]
            public SentryEvent @event = new();
        }

        public class SentryEvent
        {
            public string id = "";
            public string? groupID;
            public string eventID = "";
            public string projectID = "";
            public string? message;
            public string title = "";
            public string? location = "";
            public SentryUser? user;
            public List<SentryTag> tags = [];
            public string platform = "";
            public DateTimeOffset? dateReceived;
            public DateTimeOffset dateCreated;
            public Dictionary<string, object>? contexts;
            public int? size;
            public List<SentryEntry>? entries;
            public string? dist;
            public SentrySDK sdk = new();
            public object? context;
            public Dictionary<string, string> packages = [];
            public string type = "";
            public object metadata = new();
            public List<object> errors = [];
            public object occurence = new();
            public string? crashFile;
            public string? culprit;
            public List<string> fingerprints = [];
            public object groupingConfig = new();
            public object measurements = new();
            public object breakdowns = new();
        }

        public class SentryUser
        {
            public string? id;
            public string? email;
            public string? username;
            public string? ip_address;
            public string? name;
            public Dictionary<string, string>? data;
        }

        public class SentryTag
        {
            public string query = "";
            public string key = "";
            public string value = "";
        }

        public class SentrySDK
        {
            public string name = "";
            public string version = "";
        }

        public class SentryEntry
        {
            public SentryEntryData data = new();
        }

        public class SentryEntryData
        {
            public List<SentryException>? values = [];
        }

        public class SentryException
        {
            public string type = "";
            public string value = "";
            public string module = "";
            public SentryStacktrace stacktrace = new();
        }

        public class SentryStacktrace
        {
            public List<SentryFrame> frames = [];
        }

        public class SentryFrame
        {
            public string filename = "";
            public string absPath = "";
            public string function = "";
            public int lineNo;
            public int? colNo = -1;
            public bool inApp;
        }
    }
}
