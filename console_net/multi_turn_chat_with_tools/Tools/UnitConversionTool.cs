// UnitConversionTool.cs
// Offline unit conversions: length, mass, temperature, speed, volume, area, pressure, energy.
// Compatible with netstandard2.0 and no nullable reference types.

using LMKit.Agents.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace multi_turn_chat_with_tools.Tools
{
    public sealed class UnitConversionTool : ITool
    {
        // ---- Tool metadata / schema ----
        private const string SchemaJson = @"
{
  ""type"": ""object"",
  ""description"": ""Convert values between common unit systems, or list supported units by category. Runs offline."",
  ""properties"": {
    ""mode"": { ""type"": ""string"", ""enum"": [""convert"", ""list_units""], ""default"": ""convert"", ""description"": ""Operation mode: convert or list_units."" },
    ""amount"": { ""type"": ""number"", ""description"": ""Numeric value to convert (required for convert mode)."" },
    ""from"": { ""type"": ""string"", ""description"": ""Source unit symbol or name (e.g., 'km', 'mile', '°C', 'psi')."" },
    ""to"": { ""type"": ""string"", ""description"": ""Target unit symbol or name (e.g., 'm', 'ft', 'K', 'bar')."" },
    ""category"": { ""type"": ""string"", ""enum"": [""length"", ""mass"", ""temperature"", ""speed"", ""volume"", ""area"", ""pressure"", ""energy""], ""description"": ""Optional category to disambiguate similar unit names."" },
    ""round"": { ""type"": ""integer"", ""minimum"": 0, ""maximum"": 12, ""default"": 6, ""description"": ""Decimal places for the 'converted' value."" }
  },
  ""required"": [""mode""]
}";

        public string Name { get { return "convert_units"; } }
        public string Description { get { return "Offline unit converter for length/mass/temperature/speed/volume/area/pressure/energy, plus list_units mode."; } }
        public string InputSchema { get { return SchemaJson; } }

        // ---- Runtime ----
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public UnitConversionTool() { }

        public System.Threading.Tasks.Task<string> InvokeAsync(string arguments, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            var args = System.Text.Json.JsonSerializer.Deserialize<Args>(arguments, Json);
            if (args == null) throw new ArgumentException("Invalid arguments JSON for convert_units.");

            var mode = (args.Mode ?? "convert").Trim().ToLowerInvariant();
            if (mode != "convert" && mode != "list_units")
                throw new ArgumentException("mode must be 'convert' or 'list_units'.");

            if (mode == "list_units")
            {
                var list = UnitsCatalog.List(args.Category);
                var lr = new ListUnitsResult
                {
                    Source = new SourceInfo("local", null, "Offline unit catalog"),
                    Category = string.IsNullOrEmpty(args.Category) ? null : args.Category,
                    Units = list
                };
                return System.Threading.Tasks.Task.FromResult(System.Text.Json.JsonSerializer.Serialize(lr, Json));
            }

            // mode == convert
            if (double.IsNaN(args.Amount) || double.IsInfinity(args.Amount))
                throw new ArgumentException("'amount' must be a finite number.");
            if (string.IsNullOrWhiteSpace(args.From) || string.IsNullOrWhiteSpace(args.To))
                throw new ArgumentException("'from' and 'to' are required for convert mode.");

            var from = Normalize(args.From);
            var to = Normalize(args.To);
            var category = string.IsNullOrEmpty(args.Category) ? UnitsCatalog.InferCategory(from, to) : Normalize(args.Category);

            if (category.Length == 0)
                throw new ArgumentException("Could not infer category; provide a valid 'category' matching both units.");

            decimal converted;
            decimal factor = 0m; // only for linear categories; 0 means N/A (e.g., temperature)
            var linear = category != "temperature";

            if (category == "temperature")
            {
                // Use decimal arithmetic where possible
                converted = ConvertTemperature((decimal)args.Amount, from, to);
            }
            else
            {
                var fFrom = UnitsCatalog.GetFactor(category, from);
                var fTo = UnitsCatalog.GetFactor(category, to);
                // Convert: value_in_base = amount * fFrom; result = value_in_base / fTo
                var baseValue = ((decimal)args.Amount) * fFrom;
                converted = baseValue / fTo;
                factor = fFrom / fTo;
            }

            var round = ClampInt(args.Round, 0, 12, 6);
            var result = new ConvertResult
            {
                Source = new SourceInfo("local", null, "Offline unit conversion"),
                Category = category,
                Amount = (decimal)args.Amount,
                From = from,
                To = to,
                Factor = linear ? (decimal?)RoundMid(factor, Math.Min(12, round + 4)) : null,
                Converted = RoundMid(converted, round)
            };

            return System.Threading.Tasks.Task.FromResult(System.Text.Json.JsonSerializer.Serialize(result, Json));
        }

        // ---- Helpers ----
        private static string Normalize(string s)
        {
            s = s.Trim();
            // common symbols normalized
            s = s.Replace("°", "", StringComparison.Ordinal);
            return s.ToLowerInvariant();
        }

        private static int ClampInt(int value, int min, int max, int fallback)
        {
            if (value < min || value > max) return fallback;
            return value;
        }

        private static decimal RoundMid(decimal value, int digits)
        {
            if (digits < 0) digits = 0;
            if (digits > 12) digits = 12;
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        private static decimal ConvertTemperature(decimal amount, string from, string to)
        {
            // Normalize aliases
            if (from == "c") from = "celsius";
            if (from == "f") from = "fahrenheit";
            if (from == "k") from = "kelvin";
            if (to == "c") to = "celsius";
            if (to == "f") to = "fahrenheit";
            if (to == "k") to = "kelvin";

            // To Celsius
            decimal c;
            if (from == "celsius") c = amount;
            else if (from == "fahrenheit") c = (amount - 32m) * 5m / 9m;
            else if (from == "kelvin") c = amount - 273.15m;
            else throw new ArgumentException("Unsupported temperature unit: " + from);

            // From Celsius to target
            if (to == "celsius") return c;
            if (to == "fahrenheit") return c * 9m / 5m + 32m;
            if (to == "kelvin") return c + 273.15m;

            throw new ArgumentException("Unsupported temperature unit: " + to);
        }

        // ---- DTOs ----
        private sealed class Args
        {
            [JsonPropertyName("mode")] public string Mode { get; set; }
            [JsonPropertyName("amount")] public double Amount { get; set; }
            [JsonPropertyName("from")] public string From { get; set; }
            [JsonPropertyName("to")] public string To { get; set; }
            [JsonPropertyName("category")] public string Category { get; set; }
            [JsonPropertyName("round")] public int Round { get; set; }
        }

        private sealed class ConvertResult
        {
            public SourceInfo Source { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public decimal? Factor { get; set; } // null for temperature (non-linear)
            public decimal Converted { get; set; }
        }

        private sealed class ListUnitsResult
        {
            public SourceInfo Source { get; set; }
            public string Category { get; set; } // may be null in output if listing all
            public List<UnitInfo> Units { get; set; }
        }

        public sealed class SourceInfo
        {
            public SourceInfo() { }
            public SourceInfo(string name, string website, string notes)
            {
                Name = name; Website = website; Notes = notes;
            }
            public string Name { get; set; }
            public string Website { get; set; }
            public string Notes { get; set; }
        }

        public sealed class UnitInfo
        {
            public string Category { get; set; }
            public string Unit { get; set; }
            public string BaseUnit { get; set; }
            public string ExampleAliases { get; set; }
            public decimal FactorToBase { get; set; } // 1 for base unit; 0 when not linear (temperature will be omitted)
        }

        // ---- Catalog ----
        private static class UnitsCatalog
        {
            // Base units per category:
            // length->meter (m), mass->kilogram (kg), temperature->(special), speed->meter/second (m/s),
            // volume->liter (L), area->square meter (m2), pressure->pascal (Pa), energy->joule (J)

            private static readonly Dictionary<string, Dictionary<string, decimal>> Maps = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                { "length", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "m", 1m }, { "meter", 1m }, { "metre", 1m },
                        { "km", 1000m }, { "kilometer", 1000m }, { "kilometre", 1000m },
                        { "cm", 0.01m }, { "millimeter", 0.001m }, { "mm", 0.001m },
                        { "mile", 1609.344m }, { "mi", 1609.344m },
                        { "yard", 0.9144m }, { "yd", 0.9144m },
                        { "foot", 0.3048m }, { "ft", 0.3048m },
                        { "inch", 0.0254m }, { "in", 0.0254m },
                        { "nauticalmile", 1852m }, { "nmi", 1852m },
                        { "micrometer", 0.000001m }, { "micrometre", 0.000001m }, { "um", 0.000001m }
                    }
                },
                { "mass", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "kg", 1m }, { "kilogram", 1m },
                        { "g", 0.001m }, { "gram", 0.001m },
                        { "mg", 0.000001m }, { "milligram", 0.000001m },
                        { "lb", 0.45359237m }, { "pound", 0.45359237m },
                        { "oz", 0.028349523125m }, { "ounce", 0.028349523125m },
                        { "t", 1000m }, { "tonne", 1000m }, { "metricton", 1000m }
                    }
                },
                { "speed", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "m/s", 1m }, { "ms-1", 1m }, { "meterpersecond", 1m }, { "metrepersecond", 1m },
                        { "km/h", 1000m/3600m }, { "kmh", 1000m/3600m },
                        { "mph", 1609.344m/3600m },
                        { "knot", 1852m/3600m }, { "kn", 1852m/3600m }, { "kt", 1852m/3600m }
                    }
                },
                { "volume", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "l", 1m }, { "liter", 1m }, { "litre", 1m },
                        { "ml", 0.001m }, { "milliliter", 0.001m }, { "millilitre", 0.001m },
                        { "m3", 1000m }, { "cubicmeter", 1000m }, { "cubicmetre", 1000m },
                        { "gal", 3.785411784m }, { "gallon", 3.785411784m }, // US liquid gallon
                        { "qt", 0.946352946m }, { "quart", 0.946352946m },
                        { "pt", 0.473176473m }, { "pint", 0.473176473m },
                        { "cup", 0.2365882365m }
                    }
                },
                { "area", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "m2", 1m }, { "squaremeter", 1m }, { "squaremetre", 1m },
                        { "km2", 1_000_000m }, { "squarekilometer", 1_000_000m },
                        { "ft2", 0.09290304m }, { "squarefoot", 0.09290304m },
                        { "in2", 0.00064516m }, { "squareinch", 0.00064516m },
                        { "acre", 4046.8564224m },
                        { "hectare", 10000m }, { "ha", 10000m }
                    }
                },
                { "pressure", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "pa", 1m }, { "pascal", 1m },
                        { "kpa", 1000m },
                        { "bar", 100000m },
                        { "atm", 101325m },
                        { "psi", 6894.757293168m },
                        { "mmhg", 133.322387415m }, { "torr", 133.322387415m }
                    }
                },
                { "energy", new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "j", 1m }, { "joule", 1m },
                        { "kj", 1000m },
                        { "cal", 4.184m }, { "kcal", 4184m },
                        { "wh", 3600m }, { "kwh", 3_600_000m },
                        { "btu", 1055.05585262m }
                    }
                }
            };

            // aliases (whitespace/characters stripped+lowercased already by Normalize)
            private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "meter", "m" }, { "metre", "m" },
                { "kilometer", "km" }, { "kilometre", "km" },
                { "millimeter", "mm" }, { "millimetre", "mm" },
                { "mile", "mi" }, { "yard", "yd" }, { "foot", "ft" }, { "inch", "in" },
                { "nauticalmile", "nmi" }, { "micrometer", "um" }, { "micrometre", "um" },

                { "kilogram", "kg" }, { "gram", "g" }, { "milligram", "mg" },
                { "pound", "lb" }, { "ounce", "oz" }, { "tonne", "t" }, { "metricton", "t" },

                { "meterpersecond", "m/s" }, { "metrepersecond", "m/s" },
                { "kilometerperhour", "km/h" }, { "kilometreperhour", "km/h" },

                { "liter", "l" }, { "litre", "l" }, { "milliliter", "ml" }, { "millilitre", "ml" },
                { "cubicmeter", "m3" }, { "cubicmetre", "m3" }, { "gallon", "gal" }, { "quart", "qt" }, { "pint", "pt" },

                { "squaremeter", "m2" }, { "squaremetre", "m2" }, { "squarekilometer", "km2" },
                { "squarefoot", "ft2" }, { "squareinch", "in2" },

                { "pascal", "pa" },

                { "joule", "j" }
            };

            public static List<UnitInfo> List(string category)
            {
                var outList = new List<UnitInfo>();

                if (!string.IsNullOrEmpty(category))
                {
                    category = category.Trim().ToLowerInvariant();
                    if (!Maps.ContainsKey(category))
                        throw new ArgumentException("Unknown category: " + category);

                    var baseUnit = GetBaseUnit(category);
                    foreach (var kv in Maps[category])
                    {
                        outList.Add(new UnitInfo
                        {
                            Category = category,
                            Unit = kv.Key,
                            BaseUnit = baseUnit,
                            ExampleAliases = GetAliasExamples(kv.Key),
                            FactorToBase = kv.Value
                        });
                    }
                    return outList;
                }

                // All categories
                foreach (var cat in Maps.Keys)
                {
                    var baseUnit = GetBaseUnit(cat);
                    foreach (var kv in Maps[cat])
                    {
                        outList.Add(new UnitInfo
                        {
                            Category = cat,
                            Unit = kv.Key,
                            BaseUnit = baseUnit,
                            ExampleAliases = GetAliasExamples(kv.Key),
                            FactorToBase = kv.Value
                        });
                    }
                }
                // Temperature is non-linear; expose canonical names separately
                outList.Add(new UnitInfo { Category = "temperature", Unit = "celsius", BaseUnit = "celsius", ExampleAliases = "c, degc, °c", FactorToBase = 0m });
                outList.Add(new UnitInfo { Category = "temperature", Unit = "fahrenheit", BaseUnit = "celsius", ExampleAliases = "f, degf, °f", FactorToBase = 0m });
                outList.Add(new UnitInfo { Category = "temperature", Unit = "kelvin", BaseUnit = "celsius", ExampleAliases = "k", FactorToBase = 0m });

                return outList;
            }

            public static string InferCategory(string from, string to)
            {
                from = CanonicalUnit(from);
                to = CanonicalUnit(to);

                foreach (var cat in Maps.Keys)
                {
                    if (Maps[cat].ContainsKey(from) && Maps[cat].ContainsKey(to))
                        return cat;
                }

                // temperature handled by names
                if (IsTempName(from) && IsTempName(to)) return "temperature";

                return "";
            }

            public static decimal GetFactor(string category, string unit)
            {
                unit = CanonicalUnit(unit);
                Dictionary<string, decimal> map;
                if (!Maps.TryGetValue(category, out map))
                    throw new ArgumentException("Unknown category: " + category);
                decimal factor;
                if (!map.TryGetValue(unit, out factor))
                    throw new ArgumentException("Unsupported unit for " + category + ": " + unit);
                return factor;
            }

            private static string CanonicalUnit(string unit)
            {
                if (Aliases.ContainsKey(unit)) return Aliases[unit];
                return unit;
            }

            private static bool IsTempName(string u)
            {
                return u == "c" || u == "celsius" || u == "f" || u == "fahrenheit" || u == "k" || u == "kelvin";
            }

            private static string GetBaseUnit(string category)
            {
                if (category == "length") return "m";
                if (category == "mass") return "kg";
                if (category == "speed") return "m/s";
                if (category == "volume") return "l";
                if (category == "area") return "m2";
                if (category == "pressure") return "pa";
                if (category == "energy") return "j";
                if (category == "temperature") return "celsius";
                return "";
            }

            private static string GetAliasExamples(string unit)
            {
                // Provide a few example aliases (if any) to help the model
                var list = new List<string>();
                foreach (var kv in Aliases)
                {
                    if (kv.Value.Equals(unit, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(kv.Key);
                        if (list.Count >= 3) break;
                    }
                }
                return string.Join(", ", list.ToArray());
            }
        }
    }
}
