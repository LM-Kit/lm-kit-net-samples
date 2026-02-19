using LMKit.FunctionCalling;
using LMKit.Model;
using System.Text;

namespace function_calling
{
    internal class Program
    {
        static bool _isDownloading;

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
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

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4:14.7b",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(new Uri(input!.Trim().Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
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
            Console.WriteLine("0 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "0");

            Console.Clear();
            ShowInfo();
            SingleFunctionCall functionCalling = new(model)
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

                string? prompt = Console.ReadLine();

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
                    string result = callResult.Result?.ToString() ?? string.Empty;

                    WriteColor("\nResult: ", ConsoleColor.Green, result.Contains("\n"));
                    Console.WriteLine(result);
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static void FunctionCalling_BeforeMethodInvoke(object? sender, LMKit.FunctionCalling.Events.BeforeMethodInvokeEventArgs e)
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
