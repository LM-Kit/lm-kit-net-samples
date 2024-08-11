using HtmlAgilityPack;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.RegularExpressions;

namespace web_content_info_extractor_to_json
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI3_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-3.1-mini-4k-3.8b-instruct-gguf/resolve/main/Phi-3.1-mini-4k-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN2_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2-7b-instruct-gguf/resolve/main/Qwen-2-7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf";
        static bool _isDownloading;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(""); //set an optional license key here if available.
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
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (requires approximately 7 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-3 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2 7.6B (requires approximately 5.6 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH;
                    break;
                case "1":
                    modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_GEMMA2_9B_MODEL_PATH;
                    break;
                case "3":
                    modelLink = DEFAULT_PHI3_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN2_7B_MODEL_PATH;
                    break;
                default:
                    modelLink = input.Trim().Trim('"'); ;
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LLM model = new LLM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);


            Console.Clear();


            SingleTurnConversation chat = new SingleTurnConversation(model)
            {
                MaximumCompletionTokens = 256,
                MaximumContextLength = 4096,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = @"You are an expert in extracting and summarizing web content. When provided with the content of a web page, respond with a JSON formatted output that always and only includes the following fields:

'Primary Topic': The main subject or theme of the content.
'Domain or Field': The area of knowledge or industry the content belongs to.
'Language': The language in which the content is written.
'Audience': The intended or target audience for the content."
            };


            chat.Grammar = Grammar.CreateJsonGrammarFromTextFields(new string[] { "Primary Topic", "Domain or Field", "Language", "Audience" });

            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            while (true)
            {
                WriteColor($"\nEnter webpage page URI to be analyzed: ", ConsoleColor.Green, addNL: false);

                string uri = Console.ReadLine();

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

                string pageText = ExtractHtmlText(DownloadContent(new Uri(uri)));

                WriteColor("Assistant: ", ConsoleColor.Green);

                TextGenerationResult result = chat.Submit(pageText, new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
            }

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }

        private static string NormalizeSpacings(string text)
        {
            return new Regex("[ ]{2,}", RegexOptions.None).Replace(text, " ").Replace("\n ", "\n").Trim();
        }

        private static string ExtractHtmlText(string html)
        {//note Loïc: while this solution may not be optimal, it appears to be effective for the task at hand.
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            StringBuilder result = new StringBuilder();

            IEnumerable<HtmlNode> nodes = doc.DocumentNode.Descendants().Where(n =>
                                          n.NodeType == HtmlNodeType.Text &&
                                          n.ParentNode.Name != "script" &&
                                          n.ParentNode.Name != "style");

            foreach (HtmlNode node in nodes)
            {
                result.Append(node.InnerText);
            }

            return NormalizeSpacings(result.ToString());
        }

        private static string DownloadContent(Uri uri)
        {
            using var client = new HttpClient();
            return client.GetStringAsync(uri).Result;
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