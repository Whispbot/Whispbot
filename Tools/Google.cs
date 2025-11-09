using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools
{
    public static class Google
    {
        private static readonly HttpClient _client = new();
        private static readonly string? _apiKey = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY");
        private static readonly string? _generalCx = Environment.GetEnvironmentVariable("GOOGLE_SE_GENERAL_ID");
        private static readonly string? _whispCx = Environment.GetEnvironmentVariable("GOOGLE_SE_WHISP_ID");

        public static async Task<GoogleResult?> Search(string query, int numResults = 10, int startPoint = 1)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_generalCx)) return null;

            var result = await _client.GetAsync($"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_generalCx}&q={query}&num={numResults}&start={startPoint}");

            if (result.IsSuccessStatusCode)
            {
                string content = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GoogleResult>(content);
            }
            else return null;
        }

        public static async Task<GoogleResult?> WhispSearch(string query, int numResults = 10, int startPoint = 1)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_whispCx)) return null;

            var result = await _client.GetAsync($"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_whispCx}&q={query}&num={numResults}&start={startPoint}");

            if (result.IsSuccessStatusCode)
            {
                string content = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GoogleResult>(content);
            }
            else return null;
        }

        public class GoogleResult
        {
            public List<GoogleResultItem> items = [];
        }

        public class GoogleResultItem
        {
            public string title = "";
            public string link = "";
            public string snippet = "";
        }
    }
}
