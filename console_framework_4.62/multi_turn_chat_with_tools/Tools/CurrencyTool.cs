using LMKit.Agents.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace multi_turn_chat_with_tools.Tools
{
    public sealed class CurrencyTool : ITool
    {
        // ---- Tool metadata / schema ----
        private const string SchemaJson = @"
{
  ""type"": ""object"",
  ""description"": ""Convert currency using ECB rates (latest or historical) and optionally return a short trend window."",
  ""properties"": {
    ""amount"": { ""type"": ""number"", ""minimum"": 0, ""description"": ""Amount to convert."" },
    ""from"": { ""type"": ""string"", ""description"": ""ISO 4217 source currency (e.g., 'EUR', 'USD')."" },
    ""to"": { ""type"": ""string"", ""description"": ""ISO 4217 target currency (e.g., 'JPY', 'GBP')."" },
    ""date"": { ""type"": ""string"", ""description"": ""Historical date YYYY-MM-DD; omit for latest.""},
    ""includeTrend"": { ""type"": ""boolean"", ""default"": false, ""description"": ""Include a short daily rate timeseries ending on the chosen date (or latest)."" },
    ""trendDays"": { ""type"": ""integer"", ""minimum"": 2, ""maximum"": 90, ""default"": 7, ""description"": ""How many past days to include in the trend (2-90)."" },
    ""round"": { ""type"": ""integer"", ""minimum"": 0, ""maximum"": 8, ""default"": 4, ""description"": ""Decimal places for 'converted'."" }
  },
  ""required"": [""amount"", ""from"", ""to""]
}";

        public string Name { get { return "convert_currency"; } }
        public string Description { get { return "Convert an amount between currencies using ECB (Frankfurter) rates; supports historical dates and an optional trend window."; } }
        public string InputSchema { get { return SchemaJson; } }

        // ---- Runtime ----
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true
        };

        // Ensure TLS 1.2+ for HTTPS endpoints on older machines
        static CurrencyTool()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11; } catch { /* ignore */ }
        }

        public CurrencyTool(HttpClient httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
            _http.BaseAddress = new Uri("https://api.frankfurter.app/");
        }

        public async Task<string> InvokeAsync(string arguments, CancellationToken ct = default(CancellationToken))
        {
            var args = JsonSerializer.Deserialize<CurrencyArgs>(arguments, JsonOptions);
            if (args == null)
                throw new ArgumentException("Invalid arguments JSON for convert_currency.");

            // Validate/normalize
            var from = NormalizeCode(args.From);
            var to = NormalizeCode(args.To);
            if (from.Length != 3 || to.Length != 3)
                throw new ArgumentException("'from' and 'to' must be 3-letter ISO 4217 codes.");

            if (args.Amount < 0m || !IsFinite(args.Amount))
                throw new ArgumentException("'amount' must be a non-negative number.");

            // Short-circuit (same currency)
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                var same = new FxResult
                {
                    Source = new FxSource { Name = "frankfurter", Website = "https://www.frankfurter.app", Notes = "ECB reference rates, business days" },
                    Date = string.IsNullOrWhiteSpace(args.Date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : args.Date,
                    Base = from,
                    Target = to,
                    Amount = args.Amount,
                    Rate = 1m,
                    Converted = MathRound(args.Amount, args.Round),
                    Trend = null
                };
                return JsonSerializer.Serialize(same, JsonOptions);
            }

            // Build single-date (latest or historical) endpoint
            string path = string.IsNullOrWhiteSpace(args.Date)
                ? string.Format("latest?from={0}&to={1}", from, to)
                : string.Format("{0}?from={1}&to={2}", ParseDateOrToday(args.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), from, to);

            var fxResp = await GetAsync<FxSingleResponse>(path, ct);
            if (fxResp == null || fxResp.Rates == null || !fxResp.Rates.ContainsKey(to))
                throw new HttpRequestException("No rate available for " + from + "->" + to + " on '" + (fxResp != null ? fxResp.Date : (args.Date ?? "latest")) + "'.");

            var rate = fxResp.Rates[to];
            var converted = MathRound(args.Amount * rate, args.Round);

            // Optional trend
            List<TrendPoint> trend = null;
            if (args.IncludeTrend)
            {
                var end = ParseDateOrToday(fxResp != null ? fxResp.Date : args.Date);
                int days = Clamp(args.TrendDays, 2, 90);
                var start = end.AddDays(-days + 1);

                // Timeseries endpoint: /YYYY-MM-DD..YYYY-MM-DD?from=USD&to=EUR
                var seriesPath = string.Format("{0:yyyy-MM-dd}..{1:yyyy-MM-dd}?from={2}&to={3}", start, end, from, to);
                var seriesResp = await GetAsync<FxSeriesResponse>(seriesPath, ct);

                if (seriesResp != null && seriesResp.Rates != null)
                {
                    trend = seriesResp.Rates
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new TrendPoint
                        {
                            Date = kv.Key,
                            Rate = (kv.Value != null && kv.Value.ContainsKey(to)) ? (decimal?)kv.Value[to] : null
                        })
                        .Where(tp => tp.Rate.HasValue)
                        .ToList();
                }
            }

            var result = new FxResult
            {
                Source = new FxSource { Name = "frankfurter", Website = "https://www.frankfurter.app", Notes = "ECB reference rates, business days" },
                Date = fxResp.Date,
                Base = from,
                Target = to,
                Amount = args.Amount,
                Rate = rate,
                Converted = converted,
                Trend = trend
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }

        // ---- Helpers ----
        private static bool IsFinite(decimal d) { return d != decimal.MaxValue && d != decimal.MinValue; }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            return code.Trim().ToUpperInvariant();
        }

        private static DateTime ParseDateOrToday(string date)
        {
            DateTime d;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                return d.Date;
            }
            return DateTime.UtcNow.Date;
        }

        private static decimal MathRound(decimal value, int digits)
        {
            return Math.Round(value, Clamp(digits, 0, 8), MidpointRounding.AwayFromZero);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private async Task<T> GetAsync<T>(string path, CancellationToken ct)
        {
            using (var resp = await _http.GetAsync(path, ct))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    throw new HttpRequestException(string.Format("FX request failed: {0} {1}. Body: {2}", (int)resp.StatusCode, resp.ReasonPhrase, msg));
                }
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
        }

        // ---- DTOs ----
        public sealed class CurrencyArgs
        {
            [JsonPropertyName("amount")] public decimal Amount { get; set; }
            [JsonPropertyName("from")] public string From { get; set; }
            [JsonPropertyName("to")] public string To { get; set; }
            [JsonPropertyName("date")] public string Date { get; set; }
            [JsonPropertyName("includeTrend")] public bool IncludeTrend { get; set; }
            [JsonPropertyName("trendDays")] public int TrendDays { get; set; }
            [JsonPropertyName("round")] public int Round { get; set; }

            public CurrencyArgs()
            {
                IncludeTrend = false;
                TrendDays = 7;
                Round = 4;
            }
        }

        internal sealed class FxSingleResponse
        {
            [JsonPropertyName("amount")] public decimal Amount { get; set; }
            [JsonPropertyName("base")] public string Base { get; set; }
            [JsonPropertyName("date")] public string Date { get; set; }
            [JsonPropertyName("rates")] public Dictionary<string, decimal> Rates { get; set; }
        }

        internal sealed class FxSeriesResponse
        {
            [JsonPropertyName("start_date")] public string StartDate { get; set; }
            [JsonPropertyName("end_date")] public string EndDate { get; set; }
            // rates: { "2025-09-18": { "EUR": 0.92 }, ... }
            [JsonPropertyName("rates")] public Dictionary<string, Dictionary<string, decimal>> Rates { get; set; }
        }

        public sealed class FxResult
        {
            public FxSource Source { get; set; }
            public string Date { get; set; }
            public string Base { get; set; }
            public string Target { get; set; }
            public decimal Amount { get; set; }
            public decimal Rate { get; set; }
            public decimal Converted { get; set; }
            public List<TrendPoint> Trend { get; set; }
        }

        public sealed class FxSource
        {
            public string Name { get; set; }
            public string Website { get; set; }
            public string Notes { get; set; }
        }

        public sealed class TrendPoint
        {
            public string Date { get; set; }
            public decimal? Rate { get; set; }
        }
    }
}
