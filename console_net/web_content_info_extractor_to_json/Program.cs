using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace web_content_info_extractor_to_json
{
    internal class Program
    {
        private static bool _isDownloading;

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();

            WriteColor("*******************************************************************************************************\n" +
                       "* In this demo, we are extracting and summarizing web content into a JSON formatted output.           *\n" +
                       "* For each provided web page URI, the agent will output the following information, formatted in JSON: *\n" +
                       "* - 'Primary Topic': The main subject or theme of the content.                                        *\n" +
                       "* - 'Domain or Field': The area of knowledge or industry the content belongs to.                      *\n" +
                       "* - 'Language': The language in which the content is written.                                         *\n" +
                       "* - 'Audience': The intended or target audience for the content.                                      *\n" +
                       "*******************************************************************************************************\n", ConsoleColor.Blue);

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 4B          (~5.7 GB VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3 8B         (~6.5 GB VRAM)");
            Console.WriteLine("2 - Google Gemma 3 12B         (~11 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B      (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B         (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B    (~18 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen 3.5 27B      (~18 GB VRAM)");
            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            LM model = LoadModel(input);

            Console.Clear();

            SingleTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 256,
                MaximumInputTokens = model.GpuLayerCount > 0 ? 3840 : 1024,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = @"You are an expert in extracting and summarizing web content. When provided with the content of a web page, respond with a JSON formatted output that always and only includes the following fields:

'Primary Topic': The main subject or theme of the content.
'Domain or Field': The area of knowledge or industry the content belongs to.
'Language': The language in which the content is written.
'Audience': The intended or target audience for the content.",
                Grammar = Grammar.CreateJsonGrammarFromTextFields(new string[] { "Primary Topic", "Domain or Field", "Language", "Audience" })
            };

            chat.AfterTextCompletion += (sender, e) =>
            {
                Console.ForegroundColor = e.SegmentType switch
                {
                    LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                    LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };
                Console.Write(e.Text);
            };

            while (true)
            {
                WriteColor($"\nEnter webpage page URI to be analyzed: ", ConsoleColor.Green, addNL: false);

                string? uri = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(uri))
                {
                    break;
                }
                else if (uri.StartsWith("www."))
                {
                    uri = "https://" + uri;
                }

                if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                {
                    Console.Write($"\nThe provided URI is not correctly formatted.");
                    continue;
                }

                try
                {
                    string pageText = ExtractHtmlText(DownloadContent(new Uri(uri)));

                    WriteColor("Assistant: ", ConsoleColor.Green);

                    TextGenerationResult result = chat.Submit(pageText, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                    Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
                }
                catch (Exception e)
                {
                    WriteColor("Error: " + e.Message, ConsoleColor.Red);
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(input.Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double percent = (double)bytesRead / contentLength.Value * 100;
                Console.Write($"\rDownloading: {percent:F1}%   ");
            }
            else
            {
                Console.Write($"\rDownloading: {bytesRead / 1024.0 / 1024.0:F1} MB   ");
            }
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading: {progress * 100:F0}%   ");
            return true;
        }

        private static string ExtractHtmlText(string html)
        {
            LMKit.Data.Attachment attachment = new(Encoding.UTF8.GetBytes(html), "page.html");
            string text = attachment.GetText();
            return text;
        }

        private static string DownloadContent(Uri uri)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Other");

            string content = client.GetStringAsync(uri).Result;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("an empty response has been received from: " + uri.AbsoluteUri);
            }

            return content;
        }

        private static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.Write(text);
            }
            Console.ResetColor();
        }
    }
}
