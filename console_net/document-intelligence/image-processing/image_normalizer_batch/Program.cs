using LMKit.Media.Image;
using System.Text;

namespace image_normalizer_batch
{
    internal class Program
    {
        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();
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
                        NormalizeSingle();
                        break;
                    case "2": case "folder":
                        NormalizeFolder();
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

        record Options(int LongSide, int Rotate, bool AutoCrop, bool ProduceThumb, bool ProduceRotated, bool ProduceCropped);

        static Options PromptOptions()
        {
            Console.Write("Thumbnail long-side in pixels (blank = skip): ");
            int longSide = 0;
            bool thumb = false;
            string? raw = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed) && parsed > 0) { longSide = parsed; thumb = true; }

            Console.Write("Rotate degrees [0,90,180,270] (default 0): ");
            int rot = 0;
            if (int.TryParse(Console.ReadLine(), out int r) && (r == 90 || r == 180 || r == 270)) { rot = r; }

            Console.Write("Auto-crop uniform borders? (y/N): ");
            bool crop = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");

            return new Options(longSide, rot, crop, thumb, rot != 0, crop);
        }

        static void NormalizeSingle()
        {
            Console.WriteLine();
            Console.Write("Path to an image: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }

            Options opts = PromptOptions();
            Console.Write("Output directory (blank = same as input): ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.GetDirectoryName(path); }
            Directory.CreateDirectory(outDir!);

            Process(path, outDir!, opts);
        }

        static void NormalizeFolder()
        {
            Console.WriteLine();
            Console.Write("Path to a folder of images: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Recurse into subfolders? (y/N): ");
            bool recurse = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");

            Options opts = PromptOptions();
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };
            SearchOption opt = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.EnumerateFiles(dir, "*.*", opt)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No images found."); return; }

            int done = 0;
            foreach (string p in files)
            {
                try
                {
                    Process(p, outDir, opts);
                    done++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
            Console.WriteLine($"Processed {done}/{files.Length} image(s).");
            Console.WriteLine();
        }

        static void Process(string inputPath, string outDir, Options opts)
        {
            using ImageBuffer src = ImageBuffer.LoadAsRGB(inputPath);
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            Console.WriteLine($"  {Path.GetFileName(inputPath),-40} {src.Width}x{src.Height}");

            if (opts.ProduceThumb && opts.LongSide > 0)
            {
                int tw, th;
                if (src.Width >= src.Height) { tw = opts.LongSide; th = (int)((double)opts.LongSide / src.Width * src.Height); }
                else { th = opts.LongSide; tw = (int)((double)opts.LongSide / src.Height * src.Width); }
                using ImageBuffer thumb = src.Resize(tw, th);
                string thumbPath = Path.Combine(outDir, $"{baseName}_thumb.png");
                thumb.SaveAsPng(thumbPath);
                Console.WriteLine($"      thumb -> {Path.GetFileName(thumbPath)} ({tw}x{th})");
            }
            if (opts.ProduceRotated && opts.Rotate != 0)
            {
                using ImageBuffer rotated = src.Rotate(opts.Rotate);
                string rotPath = Path.Combine(outDir, $"{baseName}_rot{opts.Rotate}.png");
                rotated.SaveAsPng(rotPath);
                Console.WriteLine($"      rot{opts.Rotate,-3} -> {Path.GetFileName(rotPath)} ({rotated.Width}x{rotated.Height})");
            }
            if (opts.ProduceCropped && opts.AutoCrop)
            {
                using ImageBuffer cropped = src.CropAuto(margin: 4);
                string cropPath = Path.Combine(outDir, $"{baseName}_crop.png");
                cropped.SaveAsPng(cropPath);
                Console.WriteLine($"      crop  -> {Path.GetFileName(cropPath)} ({cropped.Width}x{cropped.Height})");
            }
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Image Normalizer Batch                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Resize, rotate, and auto-crop images in bulk for vision/OCR pipelines.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Normalize a single image (interactive options)");
            Console.WriteLine("  2 / folder   Normalize every image in a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
