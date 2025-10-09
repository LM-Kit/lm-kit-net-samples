using System.Text;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;

namespace web_content_info_extractor_to_json
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk?download=true";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN3_06B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-0.6b-instruct-gguf/resolve/main/Qwen3-0.6B-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GRANITE_4_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/granite-4.0-h-tiny-gguf/resolve/main/Granite-4.0-H-Tiny-64x994M-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf?download=true";
        static readonly string DEFAULT_LLAMA_3_2_1B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.2-1b-instruct.gguf/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf?download=true";
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
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B Medium (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Alibaba Qwen-3 0.6B (requires approximately 0.8 GB of VRAM)");
            Console.WriteLine("6 - Meta Llama 3.2 1B (requires approximately 1 GB of VRAM)");
            Console.WriteLine("7 - Microsoft Phi-4 14.7B Mini (requires approximately 11 GB of VRAM)");
            Console.WriteLine("8 - IBM Granite 4 7B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("9 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
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
                    modelLink = DEFAULT_GEMMA3_4B_MODEL_PATH;
                    break;
                case "3":
                    modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN3_8B_MODEL_PATH;
                    break;
                case "5":
                    modelLink = DEFAULT_QWEN3_06B_MODEL_PATH;
                    break;
                case "6":
                    modelLink = DEFAULT_LLAMA_3_2_1B_MODEL_PATH;
                    break;
                case "7":
                    modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH;
                    break;
                case "8":
                    modelLink = DEFAULT_GRANITE_4_7B_MODEL_PATH;
                    break;
                case "9":
                    modelLink = DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH;
                    break;
                default:
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);


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

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LMKit.TextGeneration.Chat.TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write(e.Text);
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