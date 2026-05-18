using LMKit.Embeddings;
using LMKit.Media.Image;
using LMKit.Model;
using System.Text;

namespace multimodal_embeddings
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

            Console.WriteLine("Loading nomic-embed-text ...");
            using LM textModel = LM.LoadFromModelID("nomic-embed-text",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine("Loading nomic-embed-vision ...");
            using LM visionModel = LM.LoadFromModelID("nomic-embed-vision",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();
            Embedder textEmbedder = new(textModel);
            Embedder imageEmbedder = new(visionModel);

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
                    case "1": case "matrix":
                        SimilarityMatrix(textEmbedder, imageEmbedder);
                        break;
                    case "2": case "search":
                        TextOverImages(textEmbedder, imageEmbedder);
                        break;
                    case "3": case "tag":
                        ImageOverTags(textEmbedder, imageEmbedder);
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

        static void SimilarityMatrix(Embedder text, Embedder image)
        {
            Console.WriteLine();
            Console.WriteLine("Paste captions one per line, blank ends:");
            List<string> captions = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  caption [{captions.Count + 1}]: ");
                Console.ResetColor();
                string? line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) { break; }
                captions.Add(line.Trim());
            }
            if (captions.Count == 0) { Console.WriteLine("No captions."); return; }

            Console.WriteLine("Paste image paths one per line, blank ends:");
            List<string> images = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  image [{images.Count + 1}]: ");
                Console.ResetColor();
                string? line = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(line)) { break; }
                if (!File.Exists(line)) { Console.WriteLine($"    (not found) {line}"); continue; }
                images.Add(line);
            }
            if (images.Count == 0) { Console.WriteLine("No images."); return; }

            float[][] tVecs = text.GetEmbeddings(captions.ToArray());
            float[][] iVecs = images.Select(p => { using ImageBuffer img = ImageBuffer.LoadAsRGB(p); return image.GetEmbeddings(img); }).ToArray();

            Console.WriteLine();
            Console.WriteLine("Cosine similarity matrix:");
            Console.Write(new string(' ', 38));
            foreach (string p in images) { Console.Write($"{Truncate(Path.GetFileName(p), 14),14}  "); }
            Console.WriteLine();
            for (int t = 0; t < captions.Count; t++)
            {
                Console.Write($"  {Truncate(captions[t], 36),-36}");
                for (int i = 0; i < images.Count; i++)
                {
                    Console.Write($"{Embedder.GetCosineSimilarity(tVecs[t], iVecs[i]),14:F3}  ");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        static void TextOverImages(Embedder text, Embedder image)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of images: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Top-K (default 5): ");
            int.TryParse(Console.ReadLine(), out int k);
            if (k <= 0) { k = 5; }

            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };
            string[] images = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (images.Length == 0) { Console.WriteLine("No images."); return; }

            Console.WriteLine($"Embedding {images.Length} image(s) ...");
            float[][] iVecs = images.Select(p => { using ImageBuffer img = ImageBuffer.LoadAsRGB(p); return image.GetEmbeddings(img); }).ToArray();

            while (true)
            {
                Console.WriteLine();
                Console.Write("Text query (blank to return to menu): ");
                string? q = Console.ReadLine();
                if (string.IsNullOrEmpty(q)) { return; }
                float[] qVec = text.GetQueryEmbeddings(q);
                (string path, float score)[] ranked = images
                    .Select((p, i) => (p, Embedder.GetCosineSimilarity(qVec, iVecs[i])))
                    .OrderByDescending(t => t.Item2).Take(k).ToArray();
                Console.WriteLine();
                Console.WriteLine($"Top-{k} matches for \"{q}\":");
                foreach (var (path, score) in ranked) { Console.WriteLine($"  {score:F3}  {path}"); }
            }
        }

        static void ImageOverTags(Embedder text, Embedder image)
        {
            Console.WriteLine();
            Console.Write("Path to an image: ");
            string? imgPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath)) { Console.WriteLine("File not found."); return; }

            Console.WriteLine("Paste candidate tags one per line, blank ends:");
            List<string> tags = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  tag [{tags.Count + 1}]: ");
                Console.ResetColor();
                string? line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) { break; }
                tags.Add(line.Trim());
            }
            if (tags.Count == 0) { Console.WriteLine("No tags."); return; }
            Console.Write("Top-K (default 5): ");
            int.TryParse(Console.ReadLine(), out int k);
            if (k <= 0) { k = 5; }

            using ImageBuffer img = ImageBuffer.LoadAsRGB(imgPath);
            float[] iVec = image.GetEmbeddings(img);
            float[][] tVecs = text.GetEmbeddings(tags.ToArray());
            (string tag, float score)[] ranked = tags
                .Select((t, i) => (t, Embedder.GetCosineSimilarity(iVec, tVecs[i])))
                .OrderByDescending(t => t.Item2).Take(k).ToArray();

            Console.WriteLine();
            Console.WriteLine($"Top-{k} tags for {Path.GetFileName(imgPath)}:");
            foreach (var (tag, score) in ranked) { Console.WriteLine($"  {score:F3}  {tag}"); }
            Console.WriteLine();
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

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
            Console.WriteLine("║      Multimodal Embeddings (Text + Image)        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Text and image embeddings share a vector space. Search and tag across modalities.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / matrix   Cosine similarity matrix of typed captions vs images");
            Console.WriteLine("  2 / search   Text-over-images: type queries, rank a folder of images");
            Console.WriteLine("  3 / tag      Image-over-tags: rank candidate tags for one image");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
