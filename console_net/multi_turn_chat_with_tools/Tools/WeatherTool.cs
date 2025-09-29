// WeatherTool.cs
// A plug-and-play C# "tool" you can register with any LLM/tool-calling runtime.
// Uses the free Open-Meteo API (no API key) for current weather + short forecast.

using LMKit.Agents.Tools;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace multi_turn_chat_with_tools.Tools
{
    public sealed class WeatherTool : ITool
    {
        // ---- Tool metadata / schema ----

        private const string SchemaJson = @"
{
  ""type"": ""object"",
  ""description"": ""Retrieve current weather and an optional short-term hourly forecast. Provide a city name or lat/lon."",
  ""properties"": {
    ""location"": { ""type"": ""string"", ""description"": ""City and optional country/region (e.g., 'Toulouse' or 'Toulouse, France')."" },
    ""latitude"": { ""type"": ""number"", ""description"": ""Latitude in decimal degrees."" },
    ""longitude"": { ""type"": ""number"", ""description"": ""Longitude in decimal degrees."" },
    ""units"": { ""type"": ""string"", ""enum"": [""metric"", ""us"", ""si""], ""default"": ""metric"", ""description"": ""Unit system: metric (°C, km/h, mm), us (°F, mph, in), si (°C, m/s, mm)."" },
    ""lang"": { ""type"": ""string"", ""default"": ""en"", ""description"": ""IETF language tag for place names (e.g., 'en', 'fr')."" },
    ""includeForecast"": { ""type"": ""boolean"", ""default"": false, ""description"": ""Include a short hourly forecast."" },
    ""forecastHours"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 48, ""default"": 12, ""description"": ""How many upcoming hours to include in the forecast (1-48)."" }
  },
  ""oneOf"": [
    { ""required"": [""location""] },
    { ""required"": [""latitude"", ""longitude""] }
  ]
}";

        // ITool (LM-Kit)
        public string Name => "get_weather";
        public string Description => "Get current weather (and an optional short hourly forecast) for a city or coordinates using Open-Meteo.";
        public string InputSchema => SchemaJson;

        // ---- Runtime ----

        private readonly OpenMeteoClient _client;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public WeatherTool(HttpClient? httpClient = null)
        {
            _client = new OpenMeteoClient(httpClient ?? new HttpClient());
        }

        // ITool entry point
        public async Task<string> InvokeAsync(string arguments, CancellationToken ct = default)
        {
            var args = JsonSerializer.Deserialize<WeatherArgs>(arguments, JsonOpts)
                   ?? throw new ArgumentException("Invalid arguments JSON for get_weather.");

            // Resolve coordinates.
            // If both location and lat/lon are provided, prefer location (avoids stray numerics being misused).
            (double lat, double lon, WeatherPlace place) coords;
            if (!string.IsNullOrWhiteSpace(args.Location))
            {
                coords = await _client.GeocodeAsync(args.Location!, args.Lang ?? "en", ct);
            }
            else if (args.Latitude is double la && args.Longitude is double lo)
            {
                coords = (la, lo, new WeatherPlace(null, null, null, null));
            }
            else
            {
                throw new ArgumentException("Provide either 'location' or both 'latitude' and 'longitude'.");
            }

            // Fetch weather
            var unitPref = UnitPreferences.From(args.Units);
            var wx = await _client.GetWeatherAsync(
                coords.lat,
                coords.lon,
                unitPref,
                args.IncludeForecast ?? true,
                args.ForecastHours ?? 12,
                ct
            );

            // Compose result
            var res = new WeatherResult
            {
                Source = new("open-meteo", "https://open-meteo.com", "No API key required"),
                Place = new WeatherPlace(
                    Name: coords.place.Name,
                    Country: coords.place.Country,
                    Admin1: coords.place.Admin1,
                    Timezone: wx.Timezone
                ),
                Coordinates = new(coords.lat, coords.lon),
                Units = unitPref.ToString(),
                Current = new CurrentWeather
                {
                    Time = wx.Current.Time,
                    Temperature = wx.Current.Temperature,
                    WindSpeed = wx.Current.WindSpeed,
                    WindDirection = wx.Current.WindDirection,
                    Precipitation = wx.Current.Precipitation,
                    WeatherCode = wx.Current.WeatherCode,
                    Weather = WeatherCodeDictionary.Lookup(wx.Current.WeatherCode, args.Lang)
                },
                Forecast = wx.Forecast?.Select(h => new HourlyPoint
                {
                    Time = h.Time,
                    Temperature = h.Temperature,
                    Precipitation = h.Precipitation,
                    WeatherCode = h.WeatherCode,
                    Weather = WeatherCodeDictionary.Lookup(h.WeatherCode, args.Lang)
                }).ToList()
            };

            return JsonSerializer.Serialize(res, JsonOpts);

        }
    }

    #region Public DTOs for the tool arguments & result
    public sealed class WeatherArgs
    {
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("longitude")] public double? Longitude { get; set; }

        /// <summary>"metric" (°C, km/h, mm), "us" (°F, mph, in), "si" (°C, m/s, mm)</summary>
        [JsonPropertyName("units")] public string? Units { get; set; } = "metric";

        [JsonPropertyName("lang")] public string? Lang { get; set; } = "en";
        [JsonPropertyName("includeForecast")] public bool? IncludeForecast { get; set; } = false;
        [JsonPropertyName("forecastHours")] public int? ForecastHours { get; set; } = 12;
    }

    public sealed record WeatherResult
    {
        public WeatherSource Source { get; init; } = default!;
        public WeatherPlace Place { get; init; } = default!;
        public LatLon Coordinates { get; init; }
        public string Units { get; init; } = "metric";
        public CurrentWeather Current { get; init; } = default!;
        public List<HourlyPoint>? Forecast { get; init; }
    }

    public sealed record WeatherSource(string Name, string? Website, string? Notes);
    public sealed record WeatherPlace(string? Name, string? Country, string? Admin1, string? Timezone);
    public readonly record struct LatLon(double Latitude, double Longitude);

    public sealed class CurrentWeather
    {
        public DateTimeOffset Time { get; set; }
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double? Precipitation { get; set; }
        public int WeatherCode { get; set; }
        public string Weather { get; set; } = string.Empty;
    }

    public sealed class HourlyPoint
    {
        public DateTimeOffset Time { get; set; }
        public double Temperature { get; set; }
        public double? Precipitation { get; set; }
        public int WeatherCode { get; set; }
        public string Weather { get; set; } = string.Empty;
    }
    #endregion

    #region Open-Meteo client (geocoding + forecast)
    internal sealed class OpenMeteoClient
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public OpenMeteoClient(HttpClient http) => _http = http;

        public async Task<(double lat, double lon, WeatherPlace place)> GeocodeAsync(string query, string lang, CancellationToken ct)
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=1&language={Uri.EscapeDataString(lang)}&format=json";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Geocoding failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {msg}");
            }

            var doc = await JsonSerializer.DeserializeAsync<GeocodeResponse>(await resp.Content.ReadAsStreamAsync(ct), Json, ct)
                      ?? throw new InvalidOperationException("Unexpected geocoding payload.");
            if (doc.Results is null || doc.Results.Count == 0)
                throw new InvalidOperationException($"Location not found: '{query}'. Try a more specific query, e.g., 'City, Country'.");

            var r = doc.Results[0];
            return (r.Latitude, r.Longitude, new WeatherPlace(r.Name, r.Country, r.Admin1, r.Timezone));
        }

        public async Task<WeatherRaw> GetWeatherAsync(double lat, double lon, UnitPreferences units, bool includeForecast, int forecastHours, CancellationToken ct)
        {
            // Normalize forecast window
            if (forecastHours < 1) forecastHours = 1;
            if (forecastHours > 48) forecastHours = 48;

            // Validate ranges (also catches accidental postal codes)
            if (double.IsNaN(lat) || lat < -90 || lat > 90)
                throw new ArgumentOutOfRangeException(nameof(lat), $"Latitude must be between -90 and 90. Got: {lat}");
            if (double.IsNaN(lon) || lon < -180 || lon > 180)
                throw new ArgumentOutOfRangeException(nameof(lon), $"Longitude must be between -180 and 180. Got: {lon}");

            var sb = new StringBuilder();
            sb.Append("https://api.open-meteo.com/v1/forecast?");
            sb.Append($"latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}");
            sb.Append("&current_weather=true");
            sb.Append("&timezone=auto");
            sb.Append(units.ToQuery());
            if (includeForecast)
            {
                sb.Append("&hourly=temperature_2m,precipitation,weathercode");
            }

            var url = sb.ToString();
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Forecast failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {msg}");
            }

            var raw = await JsonSerializer.DeserializeAsync<ForecastResponse>(await resp.Content.ReadAsStreamAsync(ct), Json, ct)
                      ?? throw new InvalidOperationException("Unexpected forecast payload.");

            var current = new CurrentWeather
            {
                Time = ParseTime(raw.CurrentWeather?.Time, raw.Timezone) ?? DateTimeOffset.UtcNow,
                Temperature = raw.CurrentWeather?.Temperature ?? double.NaN,
                WindSpeed = raw.CurrentWeather?.Windspeed ?? double.NaN,
                WindDirection = raw.CurrentWeather?.Winddirection ?? double.NaN,
                WeatherCode = raw.CurrentWeather?.Weathercode ?? -1,
                // For a quick sense of precip unit, keep first hourly value if present
                Precipitation = raw.Hourly?.Precipitation?.FirstOrDefault()
            };

            List<HourlyPoint>? forecast = null;
            if (includeForecast && raw.Hourly is not null && raw.Hourly.Time is not null)
            {
                var len = Math.Min(forecastHours, raw.Hourly.Time.Count);
                forecast = new List<HourlyPoint>(len);
                for (int i = 0; i < len; i++)
                {
                    forecast.Add(new HourlyPoint
                    {
                        Time = ParseTime(raw.Hourly.Time[i], raw.Timezone) ?? DateTimeOffset.UtcNow,
                        Temperature = SafeAt(raw.Hourly.Temperature2M, i),
                        Precipitation = SafeAt(raw.Hourly.Precipitation, i),
                        WeatherCode = (int)SafeAt(raw.Hourly.Weathercode, i)
                    });
                }
            }

            return new WeatherRaw(raw.Timezone, current, forecast);

            static double SafeAt(List<double>? list, int i) => (list != null && i < list.Count) ? list[i] : double.NaN;

            static DateTimeOffset? ParseTime(string? text, string? tz)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;

                // Open-Meteo returns local times when timezone=auto; no explicit offset in string.
                // Parse as DateTime then attach local machine offset.
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                {
                    return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
                }
                return null;
            }
        }
    }

    internal readonly record struct WeatherRaw(string? Timezone, CurrentWeather Current, List<HourlyPoint>? Forecast);

    #region Wire models to Open-Meteo JSON
    internal sealed class GeocodeResponse
    {
        [JsonPropertyName("results")] public List<GeocodeResult>? Results { get; set; }
    }

    internal sealed class GeocodeResult
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
    }

    internal sealed class ForecastResponse
    {
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }

        [JsonPropertyName("current_weather")] public CurrentWeatherBlock? CurrentWeather { get; set; }
        [JsonPropertyName("hourly")] public HourlyBlock? Hourly { get; set; }
    }

    internal sealed class CurrentWeatherBlock
    {
        [JsonPropertyName("time")] public string? Time { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("windspeed")] public double Windspeed { get; set; }
        [JsonPropertyName("winddirection")] public double Winddirection { get; set; }
        [JsonPropertyName("weathercode")] public int Weathercode { get; set; }
    }

    internal sealed class HourlyBlock
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("temperature_2m")] public List<double>? Temperature2M { get; set; }
        [JsonPropertyName("precipitation")] public List<double>? Precipitation { get; set; }
        [JsonPropertyName("weathercode")] public List<double>? Weathercode { get; set; }
    }
    #endregion
    #endregion

    #region Unit preferences & weather code mapping
    internal readonly record struct UnitPreferences(string TemperatureUnit, string WindSpeedUnit, string PrecipitationUnit)
    {
        public static UnitPreferences From(string? key) => key?.ToLowerInvariant() switch
        {
            "us" => new("fahrenheit", "mph", "inch"),
            "si" => new("celsius", "ms", "mm"),
            _ => new("celsius", "kmh", "mm") // metric default
        };

        public string ToQuery() => $"&temperature_unit={TemperatureUnit}&windspeed_unit={WindSpeedUnit}&precipitation_unit={PrecipitationUnit}";

        public override string ToString() => TemperatureUnit switch
        {
            "fahrenheit" => "us",
            "ms" => "si",
            _ => "metric"
        };
    }

    internal static class WeatherCodeDictionary
    {
        private static readonly Dictionary<int, string> En = new()
        {
            [0] = "Clear sky",
            [1] = "Mainly clear",
            [2] = "Partly cloudy",
            [3] = "Overcast",
            [45] = "Fog",
            [48] = "Depositing rime fog",
            [51] = "Light drizzle",
            [53] = "Moderate drizzle",
            [55] = "Dense drizzle",
            [56] = "Light freezing drizzle",
            [57] = "Dense freezing drizzle",
            [61] = "Slight rain",
            [63] = "Moderate rain",
            [65] = "Heavy rain",
            [66] = "Light freezing rain",
            [67] = "Heavy freezing rain",
            [71] = "Slight snowfall",
            [73] = "Moderate snowfall",
            [75] = "Heavy snowfall",
            [77] = "Snow grains",
            [80] = "Slight rain showers",
            [81] = "Moderate rain showers",
            [82] = "Violent rain showers",
            [85] = "Slight snow showers",
            [86] = "Heavy snow showers",
            [95] = "Thunderstorm",
            [96] = "Thunderstorm with slight hail",
            [99] = "Thunderstorm with heavy hail"
        };

        private static readonly Dictionary<int, string> Fr = new()
        {
            [0] = "Ciel dégagé",
            [1] = "Plutôt clair",
            [2] = "Partiellement nuageux",
            [3] = "Couvert",
            [45] = "Brouillard",
            [48] = "Brouillard givrant",
            [51] = "Bruine faible",
            [53] = "Bruine modérée",
            [55] = "Bruine forte",
            [56] = "Bruine verglaçante faible",
            [57] = "Bruine verglaçante forte",
            [61] = "Pluie faible",
            [63] = "Pluie modérée",
            [65] = "Pluie forte",
            [66] = "Pluie verglaçante faible",
            [67] = "Pluie verglaçante forte",
            [71] = "Faibles chutes de neige",
            [73] = "Chutes de neige modérées",
            [75] = "Fortes chutes de neige",
            [77] = "Grains de neige",
            [80] = "Averses de pluie faibles",
            [81] = "Averses de pluie modérées",
            [82] = "Averses de pluie violentes",
            [85] = "Averses de neige faibles",
            [86] = "Averses de neige fortes",
            [95] = "Orage",
            [96] = "Orage avec faible grêle",
            [99] = "Orage avec forte grêle"
        };

        public static string Lookup(int code, string? lang)
        {
            var map = (lang != null && lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) ? Fr : En;
            return map.TryGetValue(code, out var text) ? text : $"Unknown ({code})";
        }
    }
    #endregion
}