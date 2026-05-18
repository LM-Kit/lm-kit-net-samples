// CurrencyTool.cs
// Convert currency using ECB rates via Frankfurter (no API key).
// Supports latest or historical date + optional short trend window.

using LMKit.Agents.Tools;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tool_calling_assistant.Tools
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

        // ITool (LM-Kit)

        public string Name => "convert_currency";
        public string Description => "Convert an amount between currencies using ECB (Frankfurter) rates; supports historical dates and an optional trend window.";
        public string InputSchema => SchemaJson;

        // ---- Runtime ----
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public CurrencyTool(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
            _http.BaseAddress = new Uri("https://api.frankfurter.app/");
        }

        public async Task<string> InvokeAsync(string arguments, CancellationToken ct = default)
        {
            var args = JsonSerializer.Deserialize<CurrencyArgs>(arguments, Json)
                       ?? throw new ArgumentException("Invalid arguments JSON for convert_currency.");

            // Validate/normalize
            if (args.Amount is null)
            {
                args.Amount = 1;
            }
            else if (!IsFinite(args.Amount.Value) || args.Amount < 0m)
            {
                throw new ArgumentException("'amount' must be a non-negative number.");
            }

            var from = NormalizeCode(args.From);
            var to = NormalizeCode(args.To);
            if (from.Length != 3 || to.Length != 3)
            {
                throw new ArgumentException("'from' and 'to' must be 3-letter ISO 4217 codes.");
            }

            if (from == to)
            {
                // Short-circuit (rate=1)
                var same = new FxResult
                {
                    Source = new("frankfurter", "https://www.frankfurter.app", "ECB reference rates, business days"),
                    Date = args.Date ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                    Base = from,
                    Target = to,
                    Amount = args.Amount.Value,
                    Rate = 1m,
                    Converted = MathRound(args.Amount.Value, args.Round ?? 4),
                    Trend = null
                };
                return JsonSerializer.Serialize(same, Json);
            }

            // Build single-date (latest or historical) endpoint
            string path = string.IsNullOrWhiteSpace(args.Date)
                ? $"latest?from={from}&to={to}"
                : $"{ParseDateOrToday(args.Date!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}?from={from}&to={to}";


            var fxResp = await GetAsync<FxSingleResponse>(path, ct);
            if (fxResp?.Rates is null || !fxResp.Rates.TryGetValue(to, out var rate))
            {
                throw new HttpRequestException($"No rate available for {from}->{to} on '{fxResp?.Date ?? args.Date ?? "latest"}'.");
            }

            var converted = MathRound(args.Amount.Value * rate, args.Round ?? 4);

            // Optional trend
            List<TrendPoint>? trend = null;
            if (args.IncludeTrend == true)
            {
                var end = ParseDateOrToday(fxResp?.Date);
                var start = end.AddDays(-Math.Clamp(args.TrendDays ?? 7, 2, 90) + 1);

                // Timeseries endpoint: /YYYY-MM-DD..YYYY-MM-DD?from=USD&to=EUR
                var seriesPath = $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}?from={from}&to={to}";
                var seriesResp = await GetAsync<FxSeriesResponse>(seriesPath, ct);

                if (seriesResp?.Rates is not null)
                {
                    trend = seriesResp.Rates
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new TrendPoint
                        {
                            Date = kv.Key,
                            Rate = kv.Value.TryGetValue(to, out var r) ? r : null
                        })
                        .Where(tp => tp.Rate is not null)
                        .ToList()!;
                }
            }

            var result = new FxResult
            {
                Source = new("frankfurter", "https://www.frankfurter.app", "ECB reference rates, business days"),
                Date = fxResp!.Date,
                Base = from,
                Target = to,
                Amount = args.Amount.Value,
                Rate = rate,
                Converted = converted,
                Trend = trend
            };

            return JsonSerializer.Serialize(result, Json);
        }

        // ---- Helpers ----
        private static bool IsFinite(decimal d) => d != decimal.MaxValue && d != decimal.MinValue;

        private static string NormalizeCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "";
            }

            return code.Trim().ToUpperInvariant();
        }

        private static DateOnly ParseDateOrToday(string? date)
        {
            if (!string.IsNullOrWhiteSpace(date) &&
                DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                return d;
            }
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        private static decimal MathRound(decimal value, int digits)
            => Math.Round(value, Math.Clamp(digits, 0, 8), MidpointRounding.AwayFromZero);

        private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
        {
            using var resp = await _http.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"FX request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {msg}");
            }
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(s, Json, ct);
        }

        // ---- DTOs ----
        public sealed class CurrencyArgs
        {
            [JsonPropertyName("amount")] public decimal? Amount { get; set; }
            [JsonPropertyName("from")] public string? From { get; set; }
            [JsonPropertyName("to")] public string? To { get; set; }
            [JsonPropertyName("date")] public string? Date { get; set; }
            [JsonPropertyName("includeTrend")] public bool? IncludeTrend { get; set; } = false;
            [JsonPropertyName("trendDays")] public int? TrendDays { get; set; } = 7;
            [JsonPropertyName("round")] public int? Round { get; set; } = 4;
        }

        internal sealed class FxSingleResponse
        {
            [JsonPropertyName("amount")] public decimal? Amount { get; set; }
            [JsonPropertyName("base")] public string? Base { get; set; }
            [JsonPropertyName("date")] public string Date { get; set; } = "";
            [JsonPropertyName("rates")] public Dictionary<string, decimal>? Rates { get; set; }
        }

        internal sealed class FxSeriesResponse
        {
            [JsonPropertyName("start_date")] public string? StartDate { get; set; }
            [JsonPropertyName("end_date")] public string? EndDate { get; set; }
            // rates: { "2025-09-18": { "EUR": 0.92 }, ... }
            [JsonPropertyName("rates")] public Dictionary<string, Dictionary<string, decimal>>? Rates { get; set; }
        }

        public sealed class FxResult
        {
            public FxSource Source { get; init; } = default!;
            public string Date { get; init; } = "";
            public string Base { get; init; } = "";
            public string Target { get; init; } = "";
            public decimal Amount { get; init; }
            public decimal Rate { get; init; }
            public decimal Converted { get; init; }
            public List<TrendPoint>? Trend { get; init; }
        }

        public sealed record FxSource(string Name, string? Website, string? Notes);
        public sealed class TrendPoint
        {
            public string Date { get; set; } = "";
            public decimal? Rate { get; set; }
        }
    }
}