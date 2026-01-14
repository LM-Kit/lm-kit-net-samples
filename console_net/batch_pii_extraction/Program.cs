using LMKit.Data;
using LMKit.Model;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace batch_pii_extraction
{
    internal class Program
    {
        static bool _isDownloading;

        private static readonly object _consoleLock = new();
        private const int ProgressWidth = 24;
        private const int FileWidth = 55;
        private const int PagesWidth = 5;
        private const int EntsWidth = 5;
        private const int TimeWidth = 8;
        private const int DocThrWidth = 8;
        private const int PgThrWidth = 9;

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

        static void Main(string[] args)
        {
            const string InputDirectory = @"D:\Input";
            const string OutputDirectory = @"D:\Output";

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();

            var modelCard = ModelCard.GetPredefinedModelCardByModelID(Configuration.ModelId);

            LM model = new(
                modelCard,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();

            var files = Directory.GetFiles(
                path: InputDirectory,
                searchPattern: "*.*",
                searchOption: SearchOption.AllDirectories);

            int threadCount = Configuration.GetMaxThreadCount(model);
            Stats stats = new();

            WriteColor(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Starting processing of {0} {1} using {2} {3} with backend {4}...\n\n",
                    files.Length,
                    files.Length == 1 ? "file" : "files",
                    threadCount,
                    threadCount == 1 ? "thread" : "threads",
                    LMKit.Global.Runtime.Backend),
                ConsoleColor.Cyan);

            WriteTableHeader();

            int totalFiles = files.Length;
            int processedCount = 0;

            Action<int> curIdx = y =>
            {
                string file = files[y];
                Attachment attachment = null;

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    attachment = new Attachment(file);
                    var engine = Configuration.SetupEngine(model);
                    var entities = engine.Extract(attachment);

                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    string jsonOutput = JsonSerializer.Serialize(
                        entities,
                        jsonOptions);

                    string fileOutput = BuildOutputPath(
                        file, 
                        InputDirectory, 
                        OutputDirectory);

                    File.WriteAllText(fileOutput, jsonOutput);

                    sw.Stop();

                    stats.RecordDocument(attachment, sw.Elapsed);

                    int pages = attachment.PageCount;
                    int ents = entities.Count;
                    double secs = Math.Round(sw.Elapsed.TotalSeconds, 2);

                    int done = Interlocked.Increment(ref processedCount);
                    int remaining = totalFiles - done;

                    var snap = stats.TakeSnapshot();
                    var fileDisplay = TruncateCenter(file, FileWidth);

                    string progress = string.Format(
                        CultureInfo.CurrentCulture,
                        "{0,4}/{1,-4} -{2,5} left",
                        done, totalFiles, remaining);

                    string line = string.Format(
                        CultureInfo.CurrentCulture,
                        "{0,-" + ProgressWidth + "} {1,-" + FileWidth + "} {2," + PagesWidth + "} {3," + EntsWidth + "} {4," + TimeWidth + ":0.##} {5," + DocThrWidth + ":0.##} {6," + PgThrWidth + ":0.##}",
                        progress,
                        fileDisplay,
                        pages,
                        ents,
                        secs,
                        snap.DocsPerSec,
                        snap.PagesPerSec
                    );

                    lock (_consoleLock)
                    {
                        WriteColor(line, ConsoleColor.Green);
                    }
                }
                catch (Exception e)
                {
                    int done = Interlocked.Increment(ref processedCount);
                    int remaining = totalFiles - done;

                    lock (_consoleLock)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "[{0} / {1} | {2} left] Error: processing file at '{3}'. Details: {4} Please check the file path and permissions.",
                                done, totalFiles, remaining, file, e.Message));
                        Console.ResetColor();
                    }
                }
                finally
                {
                    attachment?.Dispose();
                }
            };

            Parallel.For(0, files.Length, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, curIdx);

            stats.Stop();
            var s = stats.TakeSnapshot();

            int totalMinutes = (int)s.Elapsed.TotalMinutes;
            double totalSeconds = s.Elapsed.TotalSeconds - (totalMinutes * 60);

            Console.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                "Summary: {0} {1}, {2} {3} | Total: {4} min {5:0.##} s | Avg/doc: {6:mm\\:ss\\.ff} | Avg/page: {7:ss\\.ff} s | Throughput: {8:0.##} doc/s, {9:0.##} page/s. Press any key to exit.",
                s.Documents, s.Documents == 1 ? "document" : "documents",
                s.Pages, s.Pages == 1 ? "page" : "pages",
                totalMinutes, totalSeconds,
                s.AvgPerDocument,
                s.AvgPerPage,
                s.DocsPerSec,
                s.PagesPerSec
            ));

            _ = Console.ReadKey();
        }

        private static void WriteTableHeader()
        {
            string header = string.Format(
                CultureInfo.InvariantCulture,
                "{0,-" + ProgressWidth + "} {1,-" + FileWidth + "} {2," + PagesWidth + "} {3," + EntsWidth + "} {4," + TimeWidth + "} {5," + DocThrWidth + "} {6," + PgThrWidth + "}",
                "PROGRESS", "FILE", "PAGES", "ENTS", "TIME(s)", "DOC/s", "PAGE/s");

            string sep = new string('─', header.Length);

            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(header);
                Console.ResetColor();
                Console.WriteLine(sep);
            }
        }

        private static string TruncateCenter(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxWidth) return text ?? string.Empty;
            if (maxWidth <= 3) return text.Substring(0, maxWidth);
            int keep = maxWidth - 3;
            int front = keep / 2;
            int back = keep - front;
            return text.Substring(0, front) + "..." + text.Substring(text.Length - back);
        }

        private static void WriteColor(string value, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        private static string BuildOutputPath(
            string inputFile, 
            string inputRoot, 
            string outputRoot)
        {
            var absInputRoot = Path.GetFullPath(inputRoot);
            var absOutputRoot = Path.GetFullPath(outputRoot);
            var absInputFile = Path.GetFullPath(inputFile);
            var relative = Path.GetRelativePath(absInputRoot, absInputFile);
            var relativeJson = Path.ChangeExtension(relative, ".json");
            var outputPath = Path.Combine(absOutputRoot, relativeJson);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            return outputPath;
        }
    }
}