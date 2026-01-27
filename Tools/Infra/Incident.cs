using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Tools.Infra
{
    public static class Incident
    {
        private static HttpClient _client = new()
        {
            BaseAddress = new Uri("https://api.incident.io/"),
            DefaultRequestHeaders =
            {
                Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("INCIDENT_API_KEY"))
            }
        };

        public static async Task<(IncidentEscalationResponse?, IncidentError?)> TriggerEscalation(string title, string? description)
        {
            HttpResponseMessage response = await _client.PostAsync("/v2/escalations", new StringContent(JsonConvert.SerializeObject(new
            {
                title,
                description,
                escalation_path_id = Environment.GetEnvironmentVariable("INCIDENT_ESCALATION_ID"),
                idempotency_key = title.Split(' ').Take(3).Join("-") + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return (JsonConvert.DeserializeObject<IncidentEscalationResponse>(await response.Content.ReadAsStringAsync()), null);
            }
            return (null, JsonConvert.DeserializeObject<IncidentError>(await response.Content.ReadAsStringAsync()));
        }

        public static async Task<(IncidentEscalationResponse?, IncidentError?)> GetEscalation(string id)
        {
            HttpResponseMessage response = await _client.GetAsync($"/v2/escalations/{id}");
            if (response.IsSuccessStatusCode)
            {
                return (JsonConvert.DeserializeObject<IncidentEscalationResponse>(await response.Content.ReadAsStringAsync()), null);
            }
            return (null, JsonConvert.DeserializeObject<IncidentError>(await response.Content.ReadAsStringAsync()));
        }

        public class IncidentError
        {
            public string type = "";
            public HttpStatusCode status;
            public string request_id = "";
            public IncidentRatelimitContent? rate_limit;
            public List<IncidentErrorContent> errors = [];
        }
        public class IncidentRatelimitContent
        {
            public string name = "";
            public int limit;
            public int remaining;
            public DateTime retry_after;
        }
        public class IncidentErrorContent
        {
            public string code = "";
            public string message = "";
        }

        public class IncidentEscalationResponse
        {
            public IncidentEscalationData escalation = new();
        }
        public class IncidentEscalationData
        {
            public string id = "";
            public string title = "";
            public string status = "";
            public DateTime created_at;
            public string escalation_path_id = "";
            public IncidentPriority priority = new();
            public List<IncidentEvent> events = [];
        }
        public class IncidentPriority
        {
            public string name = "";
        }
        public class IncidentEvent
        {
            [JsonProperty("event")]
            public string @event = "";
            public List<IncidentEventUser> users = [];
        }
        public class IncidentEventUser
        {
            public string id = "";
            public string email = "";
            public string name = "";
            public string role = "";
            public string slack_user_id = "";
        }
    }
}
