using LMKit.Cryptography;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

// ---------------------------------------------------------------------------
// LM-Kit.NET - Encrypted GGUF Model Loading
//
// Demonstrates how to:
//   1. Encrypt a plaintext GGUF file into an LM-Kit encrypted container.
//   2. Load that container directly with LM.LoadEncrypted(), decrypting tensor
//      bytes on the fly from disk as the native runtime requests them.
//
// The loader never reads the full encrypted file into memory. Only the GGUF
// metadata block (a few MB at most) plus one tensor's worth of bytes at a time
// are ever held in RAM during load.
//
// Usage:
//   encrypted_model_loading                        -> interactive (picks a small model, prompts)
//   encrypted_model_loading <plaintext.gguf>       -> use given plaintext file
//   encrypted_model_loading <plaintext> <password> -> use given plaintext + password
// ---------------------------------------------------------------------------

namespace encrypted_model_loading
{
    internal class Program
    {
        private static bool _isDownloading;

        static int Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Encrypted GGUF Model Loading Demo ===");
            Console.WriteLine();

            string plaintextPath = args.Length >= 1 ? args[0] : ResolvePlaintextModel();
            if (string.IsNullOrEmpty(plaintextPath) || !File.Exists(plaintextPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Plaintext model not found: {plaintextPath ?? "<null>"}");
                Console.ResetColor();
                return 1;
            }

            string password = args.Length >= 2 ? args[1] : PromptPassword();
            string encryptedPath = Path.ChangeExtension(plaintextPath, ".lmke");

            Console.WriteLine($"Plaintext      : {plaintextPath}");
            Console.WriteLine($"Encrypted file : {encryptedPath}");
            Console.WriteLine();

            if (!File.Exists(encryptedPath) ||
                new FileInfo(encryptedPath).LastWriteTimeUtc < new FileInfo(plaintextPath).LastWriteTimeUtc)
            {
                Console.Write("Encrypting (streaming, AES-256-CTR)... ");
                DateTime t0 = DateTime.UtcNow;
                EncryptedGguf.Encrypt(plaintextPath, encryptedPath,
                    GgufEncryptionScheme.AesCtr256, password);
                Console.WriteLine($"done in {(DateTime.UtcNow - t0).TotalSeconds:F1}s.");
            }
            else
            {
                Console.WriteLine("Using existing encrypted container.");
            }
            Console.WriteLine();

            Console.Write("Loading encrypted model... ");
            DateTime t1 = DateTime.UtcNow;
            using LM model = LM.LoadEncrypted(encryptedPath,
                GgufEncryptionScheme.AesCtr256, password,
                loadingProgress: OnLoadProgress);
            Console.WriteLine($"done in {(DateTime.UtcNow - t1).TotalSeconds:F1}s. Model: {model.Name}");
            Console.WriteLine();

            Console.WriteLine("Type a prompt (blank line to exit).");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("> ");
                Console.ResetColor();
                string prompt = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(prompt)) { break; }

                var chat = new MultiTurnConversation(model) { MaximumCompletionTokens = 256 };
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(chat.Submit(prompt).Completion);
                Console.ResetColor();
                Console.WriteLine();
            }
            return 0;
        }

        // Interactive: pick a small model family and download it via LM-Kit's
        // model catalog, returning the resolved plaintext GGUF path.
        static string ResolvePlaintextModel()
        {
            Console.WriteLine("No plaintext model path was provided. Pick one:");
            Console.WriteLine("  1 - Google Gemma 3 270M Instruct  (~250 MB) [default]");
            Console.WriteLine("  2 - Microsoft Phi-4 Mini 3.8B     (~2.4 GB)");
            Console.WriteLine("  3 - Alibaba Qwen 3 4B             (~2.5 GB)");
            Console.WriteLine();
            Console.Write("Choice (1/2/3) or full path to a .gguf file: ");
            string input = Console.ReadLine()?.Trim() ?? "";

            if (File.Exists(input)) { return input; }

            string modelId = input switch
            {
                "2" => "phi4-mini:3.8b",
                "3" => "qwen3:4b",
                _   => "gemma3:270m",
            };

            Console.WriteLine();
            Console.Write($"Downloading / locating {modelId} via LM-Kit model catalog ...");
            using LM lm = LM.LoadFromModelID(modelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            // The catalog path lives under Configuration.ModelStorageDirectory.
            // We dispose the LM and return the resolved on-disk path so the demo
            // can re-encrypt from a stable file location.
            string path = lm.ModelPath ?? string.Empty;
            return File.Exists(path) ? path : string.Empty;
        }

        static string PromptPassword()
        {
            Console.Write("Enter encryption password (press Enter to use 'demo'): ");
            string pw = Console.ReadLine();
            return string.IsNullOrEmpty(pw) ? "demo" : pw;
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading {bytesRead} bytes");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            return true;
        }
    }
}
