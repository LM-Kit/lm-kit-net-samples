using LMKit.FunctionCalling;
using LMKit.Model;
using System;
using System.Text;

namespace function_calling
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk?download=true";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN2_5_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2.5-7b-instruct-gguf/resolve/main/Qwen-2.5-7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GRANITE_3_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/granite-3.3-8b-instruct-gguf/resolve/main/granite-3.3-8B-Instruct-Q4_K_M.gguf?download=true";
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
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B Medium (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2.5 7.6B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Microsoft Phi-4 14.7B Mini (requires approximately 11 GB of VRAM)");
            Console.WriteLine("6 - IBM Granite 8B (requires approximately 6 GB of VRAM)");

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
                    modelLink = DEFAULT_QWEN2_5_7B_MODEL_PATH;
                    break;
                case "5":
                    modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH;
                    break;
                case "6":
                    modelLink = DEFAULT_GRANITE_3_3_8B_MODEL_PATH;
					break;
                default:
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(modelUri,
                                     downloadingProgress: ModelDownloadingProgress,
                                     loadingProgress: ModelLoadingProgress);

            Console.Clear();
            ShowInfo();
            SingleFunctionCall functionCalling = new SingleFunctionCall(model)
            {
                InvokeFunctions = true
            };


            functionCalling.BeforeMethodInvoke += FunctionCalling_BeforeMethodInvoke;

            functionCalling.ImportFunctions<BookPlugin>();


            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n\nType your query: ");
                Console.ResetColor();

                string prompt = Console.ReadLine();

                if (string.IsNullOrEmpty(prompt))
                {
                    break;
                }

                FunctionCallResult callResult = functionCalling.Submit(prompt);


                if (callResult.Method == null)
                {
                    Console.WriteLine(">> No function has been called");
                }
                else
                {
                    string result = callResult.Result.ToString();

                    WriteColor("\nResult: ", ConsoleColor.Green, result.Contains("\n"));
                    Console.WriteLine(result);
                }
            }

            Console.WriteLine("The session. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void FunctionCalling_BeforeMethodInvoke(object sender, LMKit.FunctionCalling.Events.BeforeMethodInvokeEventArgs e)
        {
            Console.WriteLine(">> Invoking method " + e.MethodInfo.Name + "...");
        }

        private static void ShowInfo()
        {
            Console.WriteLine("-- Welcome to the  Open Library Search --");
            Console.WriteLine("\nSupported requests are:");
            Console.WriteLine("- Retrieves the count of books available by a specified author.");
            Console.WriteLine("- Retrieves the author's name for a specified book.");
            Console.WriteLine("- Retrieves detailed information about a specified book.");
            Console.WriteLine("- Retrieves detailed information about the most recent book by a specified author.");

            Console.WriteLine("\nYou can also ask questions like 'Who wrote The Lord of the Rings?' or 'Give me details about The Lord of the Rings.'\n");
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
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