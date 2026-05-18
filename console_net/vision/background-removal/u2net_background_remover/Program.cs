using LMKit.Media.Image;
using LMKit.Model;
using LMKit.Segmentation;
using System.Diagnostics;
using System.Text;

namespace u2net_background_remover
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading u2net segmentation model ...");
            using LM model = LM.LoadFromModelID("u2net",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();
            BackgroundDetection detector = new(model);

            PrintMenu();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "file":
                        RemoveSingle(detector);
                        break;
                    case "2": case "folder":
                        RemoveFolder(detector);
                        break;
                    case "q": case "quit": case "exit":
                        return;
                    case "?": case "help": case "menu":
                        PrintMenu();
                        break;
                    default:
                        Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                        break;
                }
            }
        }

        static void RemoveSingle(BackgroundDetection detector)
        {
            Console.WriteLine();
            Console.Write("Path to an image: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            Console.Write("Output PNG path (blank = <name>_nobg.png next to input): ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}_nobg.png");
            }
            Process(detector, path, output);
        }

        static void RemoveFolder(BackgroundDetection detector)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of images: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };
            string[] files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No images found."); return; }

            int done = 0;
            foreach (string p in files)
            {
                string outPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(p)}_nobg.png");
                if (Process(detector, p, outPath)) { done++; }
            }
            Console.WriteLine();
            Console.WriteLine($"Processed {done}/{files.Length} image(s).");
            Console.WriteLine();
        }

        static bool Process(BackgroundDetection detector, string input, string output)
        {
            try
            {
                using ImageBuffer source = ImageBuffer.LoadAsRGB(input);
                Stopwatch sw = Stopwatch.StartNew();
                using ImageBuffer result = detector.RemoveBackground(source);
                sw.Stop();
                if (!result.SaveAsPng(output)) { Console.WriteLine($"  [save failed] {output}"); return false; }
                Console.WriteLine($"  {Path.GetFileName(input),-40} -> {Path.GetFileName(output)} ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {Path.GetFileName(input)}: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue) { Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%"); }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      U2Net Background Remover                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Cut out the foreground subject from product or portrait images, on-device.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Remove background from a single image");
            Console.WriteLine("  2 / folder   Remove background from every image in a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
