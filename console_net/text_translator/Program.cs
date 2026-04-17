using LMKit.Media.Image;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.Translation;
using System.Text;

namespace translator
{
    internal class Program
    {
        static bool _isDownloading;

        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp" };

        static readonly (string ModelId, string Label)[] TranslationModels =
        {
            ("translategemma3:4b",  "Google TranslateGemma 3 4B      (~3 GB VRAM)  [Suggested]"),
            ("translategemma3:12b", "Google TranslateGemma 3 12B    (~8 GB VRAM)  [Suggested]"),
        };

        static readonly (string ModelId, string Label)[] GeneralModels =
        {
            ("qwen3.5:9b",    "Alibaba Qwen 3.5 9B             (~7 GB VRAM)"),
            ("gemma4:e4b",    "Google Gemma 4 E4B              (~6 GB VRAM)"),
            ("qwen3.5:27b",   "Alibaba Qwen 3.5 27B            (~18 GB VRAM)"),
        };

        static readonly (Language Lang, string Label)[] LanguageChoices =
        {
            (Language.English,           "English"),
            (Language.French,            "French"),
            (Language.Spanish,           "Spanish"),
            (Language.German,            "German"),
            (Language.Italian,           "Italian"),
            (Language.Portuguese,        "Portuguese"),
            (Language.ChineseSimplified, "Chinese (Simplified)"),
            (Language.Japanese,          "Japanese"),
            (Language.Korean,            "Korean"),
            (Language.Arabic,            "Arabic"),
            (Language.Russian,           "Russian"),
            (Language.Hindi,             "Hindi"),
        };

        static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintHeader("Text Translator Demo");
            Console.WriteLine("Translate text or extract and translate text from images.\n");

            LM model = SelectAndLoadModel();
            bool modelSupportsVision = model.HasVision;

            Console.Clear();
            PrintHeader("Text Translator Demo");

            Language destLanguage = SelectTargetLanguage();

            TextTranslation translator = new(model);
            translator.TranslationProgress += OnTranslationProgress;
            int translationCount = 0;

            PrintDivider();
            PrintStatus(modelSupportsVision
                ? "Enter text to translate, or provide an image file path."
                : "Enter text to translate.");
            PrintStatus("Type /lang to change target language, or /quit to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;

                if (translationCount > 0)
                {
                    PrintDivider();
                }

                Console.Write($"[{destLanguage}] > ");
                Console.ResetColor();

                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                string trimmedInput = input.Trim().Trim('"');

                if (trimmedInput.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (trimmedInput.Equals("/lang", StringComparison.OrdinalIgnoreCase))
                {
                    destLanguage = SelectTargetLanguage();
                    PrintDivider();
                    PrintStatus($"Target language changed to {destLanguage}.\n");
                    continue;
                }

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                try
                {
                    if (IsImagePath(trimmedInput))
                    {
                        if (!modelSupportsVision)
                        {
                            PrintError("The selected model does not support vision input. Please use a vision-capable model for image translation.");
                            continue;
                        }

                        if (!File.Exists(trimmedInput))
                        {
                            PrintError($"File not found: {trimmedInput}");
                            continue;
                        }

                        PrintStatus("Loading image...");
                        using ImageBuffer image = ImageBuffer.LoadAsRGB(trimmedInput);
                        PrintStatus($"Image loaded ({image.Width}x{image.Height}).");

                        PrintStatus("Detecting language...");
                        Language inputLanguage = translator.DetectLanguage(image, cancellationToken: cts.Token);
                        PrintStatus($"Detected: {inputLanguage}. Translating to {destLanguage}...\n");

                        Console.ResetColor();
                        _ = translator.Translate(image, destLanguage, cts.Token);
                        Console.WriteLine();
                    }
                    else
                    {
                        PrintStatus("Detecting language...");
                        Language inputLanguage = translator.DetectLanguage(trimmedInput, cancellationToken: cts.Token);
                        PrintStatus($"Detected: {inputLanguage}. Translating to {destLanguage}...\n");

                        Console.ResetColor();
                        _ = translator.Translate(trimmedInput, destLanguage, cts.Token);
                        Console.WriteLine();
                    }

                    translationCount++;
                }
                catch (OperationCanceledException)
                {
                    PrintError("\nTranslation timed out.");
                }
                catch (Exception ex)
                {
                    PrintError($"\nError: {ex.Message}");
                }
            }

            Console.ResetColor();
            Console.WriteLine("\nDemo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        static LM SelectAndLoadModel()
        {
            Console.WriteLine("Select a model:\n");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  --- Translation-specialized models ---");
            Console.ResetColor();

            int index = 0;

            foreach (var (_, label) in TranslationModels)
            {
                Console.WriteLine($"  {index++} - {label}");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  --- General-purpose models ---");
            Console.ResetColor();

            foreach (var (_, label) in GeneralModels)
            {
                Console.WriteLine($"  {index++} - {label}");
            }

            Console.Write("\n  Or enter a custom model URI\n\n> ");
            string input = Console.ReadLine()?.Trim() ?? "";

            if (int.TryParse(input, out int choice) && choice >= 0 && choice < TranslationModels.Length + GeneralModels.Length)
            {
                string modelId = choice < TranslationModels.Length
                    ? TranslationModels[choice].ModelId
                    : GeneralModels[choice - TranslationModels.Length].ModelId;

                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }

            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static Language SelectTargetLanguage()
        {
            Console.WriteLine("\nSelect target language:\n");

            for (int i = 0; i < LanguageChoices.Length; i++)
            {
                Console.WriteLine($"  {i,2} - {LanguageChoices[i].Label}");
            }

            Console.Write("\n> ");
            string? input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out int choice) && choice >= 0 && choice < LanguageChoices.Length)
            {
                return LanguageChoices[choice].Lang;
            }

            return Language.English;
        }

        static bool IsImagePath(string input)
        {
            try
            {
                string ext = Path.GetExtension(input);
                return !string.IsNullOrEmpty(ext) &&
                       Array.Exists(ImageExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"=== {title} ===\n");
            Console.ResetColor();
        }

        static void PrintStatus(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n" + new string('-', 60) + "\n");
            Console.ResetColor();
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static void OnTranslationProgress(object? sender, LMKit.Translation.Events.TranslationProgressEventArgs e)
        {
            Console.Write(e.TranslatedChunk);
        }
    }
}
