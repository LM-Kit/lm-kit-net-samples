using LMKit.Data;
using LMKit.Document.Parsing;
using LMKit.Embeddings;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace parsed_document_rag
{
    /// <summary>
    /// Question answering over a folder of documents parsed by LM-Kit.NET's AI document parser,
    /// with page citations. The parser keeps what flat text extraction loses (table columns, the
    /// data behind charts, the heading each passage belongs to), and each page is indexed as its
    /// own section, so every answer names the pages it came from.
    ///
    /// The parse runs first and is stored as JSON beside each document: a second run re-indexes
    /// from the stored parses without the parser's models.
    /// </summary>
    internal class Program
    {
        private const string EmbeddingModelId = "embeddinggemma-300m";

        private static void Main()
        {
            // Set your license key here. A trial runs without one.
            //LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== LM-Kit.NET: Q&A over Parsed Documents, with Page Citations ===");
            Console.ResetColor();
            Console.WriteLine();

            string folder = PromptFolder();

            // 1. Parse (or reuse stored parses). The parser is released before the chat model
            //    loads, so both never hold the GPU at the same time.
            List<ParsedDocument> documents = ParseFolder(folder);

            if (documents.Count == 0)
            {
                Console.WriteLine("No document to index.");
                return;
            }

            // 2. Index one section per page, with Markdown-aware chunking.
            Console.WriteLine("\nLoading the embedding model...");
            LM embeddingModel = LM.LoadFromModelID(EmbeddingModelId, downloadingProgress: OnDownloadProgress);
            var rag = new RagEngine(new Embedder(embeddingModel));
            var chunking = new MarkdownChunking { MaxChunkSize = 500 };
            int sections = 0;

            foreach (ParsedDocument document in documents)
            {
                foreach (ParsedPage page in document.Pages)
                {
                    // Running heads and feet repeat on every page: they are left out of the index.
                    string text = string.Join("\n\n", page.Body
                        .Select(element => element.ToMarkdown())
                        .Where(markdown => markdown.Length > 0));

                    if (text.Length == 0)
                    {
                        continue;
                    }

                    var metadata = new MetadataCollection();
                    metadata.Add("source", document.SourceName);
                    metadata.Add("page", page.PageNumber.ToString());

                    rag.ImportText(text, chunking, "documents", $"{document.SourceName} p.{page.PageNumber}", metadata);
                    sections++;
                }
            }

            Console.WriteLine($"Indexed {sections} page(s) from {documents.Count} document(s).\n");

            // 3. Ask.
            Console.WriteLine("Choose the chat model:");
            Console.WriteLine("  0 - Alibaba Qwen 3.5 9B      (~7 GB VRAM) [Recommended]");
            Console.WriteLine("  1 - Google Gemma 4 E4B       (~6 GB VRAM)");
            Console.WriteLine("  2 - Microsoft Phi-4 14.7B     (~11 GB VRAM)");
            Console.WriteLine("  3 - OpenAI GPT OSS 20B        (~16 GB VRAM)");
            Console.WriteLine("  4 - Z.ai GLM 4.7 Flash 30B    (~18 GB VRAM)");
            Console.WriteLine("  5 - Alibaba Qwen 3.8 27B       (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.6 35B-A3B   (~22 GB VRAM)");
            Console.WriteLine("  7 - Google Gemma 4 26B-A4B     (~18 GB VRAM)");
            Console.WriteLine("  Or enter a model ID or URI.");
            LM chatModel = PromptModel();

            var chat = new SingleTurnConversation(chatModel)
            {
                SystemPrompt = "Answer only from the provided context. When the context does not hold the answer, say so.",
                MaximumCompletionTokens = 1024
            };

            chat.AfterTextCompletion += (_, e) =>
            {
                if (e.SegmentType == TextSegmentType.UserVisible)
                {
                    Console.Write(e.Text);
                }
            };

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nQuestion (empty to quit): ");
                Console.ResetColor();
                string? question = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(question))
                {
                    break;
                }

                var matches = rag.FindMatchingPartitions(question, topK: 5, minScore: 0.3f);

                if (matches.Count == 0)
                {
                    Console.WriteLine("No passage of the documents matches the question.");
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nAnswer: ");
                Console.ResetColor();
                rag.QueryPartitions(question, matches, chat);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n\nSources:");

                foreach (string source in matches.Select(m => m.SectionIdentifier).Distinct())
                {
                    Console.WriteLine($"  {source}");
                }

                Console.ResetColor();
            }
        }

        private static List<ParsedDocument> ParseFolder(string folder)
        {
            var documents = new List<ParsedDocument>();
            var pending = new List<string>();

            foreach (string file in Directory.GetFiles(folder).Where(Attachment.IsSupportedFormat))
            {
                string stored = file + ".parsed.json";

                if (File.Exists(stored))
                {
                    documents.Add(ParsedDocument.FromJson(File.ReadAllText(stored)));
                    Console.WriteLine($"  {Path.GetFileName(file)}: stored parse reused");
                }
                else
                {
                    pending.Add(file);
                }
            }

            if (pending.Count == 0)
            {
                return documents;
            }

            const ParsingEffort Effort = ParsingEffort.Medium;

            foreach (ModelCard card in DocumentParser.GetModelCards(Effort).Where(card => !card.IsLocallyAvailable))
            {
                Console.WriteLine($"Downloading {card.ModelName} ({card.FileSize / (1024 * 1024)} MB)...");
                card.Download(OnDownloadProgress);
                Console.WriteLine();
            }

            Console.WriteLine("Loading the parser's models...");

            using (var parser = new DocumentParser(Effort))
            {
                parser.PageParsed += (_, e) => Console.Write($"\r  {e.SourceName}: page {e.PagesParsed}/{e.PageCount}   ");

                foreach (string file in pending)
                {
                    try
                    {
                        ParsedDocument document = parser.Parse(file);
                        File.WriteAllText(file + ".parsed.json", document.ToJson());
                        documents.Add(document);
                        Console.WriteLine($"\r  {Path.GetFileName(file)}: {document.Pages.Count} page(s) parsed in {document.Elapsed.TotalSeconds:F1} s");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\r  {Path.GetFileName(file)}: skipped ({ex.Message})");
                        Console.ResetColor();
                    }
                }
            }

            return documents;
        }

        private static LM PromptModel()
        {
            Console.Write("> ");
            string? input = Console.ReadLine()?.Trim();

            string modelId = input switch
            {
                "0" or "" or null => "qwen3.5:9b",
                "1" => "gemma4:e4b",
                "2" => "phi4",
                "3" => "gptoss:20b",
                "4" => "glm4.7-flash",
                "5" => "qwen3.8:27b",
                "6" => "qwen3.6:35b-a3b",
                "7" => "gemma4:26b-a4b",
                _ => input.Trim('"')
            };

            return modelId.Contains("://")
                ? new LM(new Uri(modelId), downloadingProgress: OnDownloadProgress)
                : LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            if (contentLength.HasValue && contentLength.Value > 0)
            {
                Console.Write($"\r  downloading {(double)bytesRead / contentLength.Value * 100:F1}%   ");
            }

            return true;
        }

        private static string PromptFolder()
        {
            while (true)
            {
                Console.Write("Folder of documents to index: ");
                string? input = Console.ReadLine()?.Trim().Trim('"');

                if (!string.IsNullOrEmpty(input) && Directory.Exists(input))
                {
                    return input;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Folder not found.");
                Console.ResetColor();
            }
        }
    }
}
