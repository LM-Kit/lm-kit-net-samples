using LMKit.Data;
using LMKit.Extraction;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace receipt_expense_scanner
{
    internal class Program
    {
        private static bool _isDownloading;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"
        };

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== AI Receipt & Expense Scanner ===\n");
            Console.WriteLine("Extract structured expense data from any receipt.");
            Console.WriteLine("Supports text, PDF, and scanned receipt images.");
            Console.WriteLine("All processing runs locally on your hardware.\n");

            Console.WriteLine("Select a vision-language model to use for extraction:\n");
            Console.WriteLine("0 - Z.ai GLM-V 4.6 Flash 10B  (~7 GB VRAM)");
            Console.WriteLine("1 - MiniCPM o 4.5 9B          (~5.9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 VL 2B      (~2.5 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 VL 4B      (~4.5 GB VRAM)");
            Console.WriteLine("4 - Alibaba Qwen 3 VL 8B      (~6.5 GB VRAM) [Recommended]");
            Console.WriteLine("5 - Google Gemma 3 4B          (~5.7 GB VRAM)");
            Console.WriteLine("6 - Google Gemma 3 12B         (~11 GB VRAM)");
            Console.WriteLine("7 - Alibaba Qwen 3.5 27B      (~18 GB VRAM)");
            Console.Write("\nOther: Custom model URI or model ID\n\n> ");

            string inputStr = Console.ReadLine() ?? string.Empty;
            LM model = LoadModel(inputStr);

            Console.Clear();

            var textExtraction = new TextExtraction(model);
            textExtraction.Elements = CreateElements();

            WriteColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteColor("║                AI RECEIPT & EXPENSE SCANNER                    ║", ConsoleColor.Cyan);
            WriteColor("╚═══════════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();
            Console.WriteLine("Scan any receipt and extract structured expense data instantly.");
            Console.WriteLine();
            Console.WriteLine("  sample  - Try with a built-in sample receipt");
            Console.WriteLine("  <path>  - Scan a receipt file (.txt, .pdf, .png, .jpg)");
            Console.WriteLine("  q       - Quit");
            Console.WriteLine();

            while (true)
            {
                WriteColor("Receipt> ", ConsoleColor.Green, addNL: false);
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                    break;

                if (input.Equals("sample", StringComparison.OrdinalIgnoreCase))
                {
                    string sampleReceipt = GetSampleReceipt();
                    WriteColor("\n--- Sample Receipt ---", ConsoleColor.DarkGray);
                    Console.WriteLine(sampleReceipt);
                    WriteColor("--- End of Receipt ---\n", ConsoleColor.DarkGray);
                    textExtraction.SetContent(sampleReceipt);
                }
                else
                {
                    string filePath = input.Trim('"');
                    if (!File.Exists(filePath))
                    {
                        WriteColor($"File not found: {filePath}\n", ConsoleColor.Red);
                        continue;
                    }

                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    bool isImage = ImageExtensions.Contains(ext);

                    WriteColor($"\nLoading: {Path.GetFileName(filePath)}" +
                               (isImage ? " (image, using vision model)" : ""),
                               ConsoleColor.DarkGray);

                    if (ext == ".txt")
                        textExtraction.SetContent(File.ReadAllText(filePath));
                    else
                        textExtraction.SetContent(new Attachment(filePath));
                }

                Console.WriteLine("\nExtracting expense data...\n");
                var sw = Stopwatch.StartNew();

                try
                {
                    var result = textExtraction.Parse();
                    sw.Stop();

                    WriteColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
                    WriteColor("║                    EXPENSE REPORT                             ║", ConsoleColor.Cyan);
                    WriteColor("╚═══════════════════════════════════════════════════════════════╝\n", ConsoleColor.Cyan);

                    foreach (var element in result.Elements)
                    {
                        Console.Write($"  {element.TextExtractionElement.Name}: ");
                        WriteColor(element.ToString(), ConsoleColor.Blue, addNL: false);
                        Console.WriteLine();
                    }

                    WriteColor("\n--- JSON Output ---\n", ConsoleColor.DarkGray);
                    Console.WriteLine(result.Json);
                    WriteColor("--- End JSON ---", ConsoleColor.DarkGray);

                    WriteColor($"\nExtraction completed in {sw.Elapsed.TotalSeconds:F1} seconds.\n", ConsoleColor.Green);
                }
                catch (Exception ex)
                {
                    WriteColor($"\nError: {ex.Message}\n", ConsoleColor.Red);
                }
            }

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        private static List<TextExtractionElement> CreateElements()
        {
            return new List<TextExtractionElement>
            {
                new TextExtractionElement("Store Name", ElementType.String, "Name of the store or business."),
                new TextExtractionElement("Store Address", ElementType.String, "Full address of the store."),
                new TextExtractionElement("Date", ElementType.Date, "Date of the transaction."),
                new TextExtractionElement("Time", ElementType.String, "Time of the transaction."),

                new TextExtractionElement(
                    "Items",
                    new List<TextExtractionElement>
                    {
                        new("Description", ElementType.String, "Item name or description."),
                        new("Quantity", ElementType.Integer, "Quantity purchased."),
                        new("Unit Price", ElementType.Float, "Price per unit."),
                        new("Total", ElementType.Float, "Line total for this item.")
                    },
                    isArray: true,
                    "List of purchased items."
                ),

                new TextExtractionElement("Subtotal", ElementType.Float, "Total before tax and discounts."),
                new TextExtractionElement("Tax Rate", ElementType.String, "Tax rate applied (e.g. 8.5%)."),
                new TextExtractionElement("Tax Amount", ElementType.Float, "Total tax amount."),
                new TextExtractionElement("Discount", ElementType.Float, "Total discount applied, 0 if none."),
                new TextExtractionElement("Total", ElementType.Float, "Final total amount paid."),
                new TextExtractionElement("Payment Method", ElementType.String, "Payment method used (cash, card, etc.)."),
                new TextExtractionElement("Transaction ID", ElementType.String, "Transaction or receipt reference number."),
                new TextExtractionElement("Expense Category", ElementType.String,
                    "Suggested expense category: Meals, Office Supplies, Travel, Entertainment, Groceries, or Other.")
            };
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "glm-4.6v-flash",
                "1" => "minicpm-o-45",
                "2" => "qwen3-vl:2b",
                "3" => "qwen3-vl:4b",
                "4" => "qwen3-vl:8b",
                "5" => "gemma3:4b",
                "6" => "gemma3:12b",
                "7" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3-vl:8b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        private static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
                Console.WriteLine(text);
            else
                Console.Write(text);
            Console.ResetColor();
        }

        private static string GetSampleReceipt()
        {
            return @"
========================================
         WHOLE FOODS MARKET
       399 4th Street
       San Francisco, CA 94107
       Tel: (415) 618-0066
========================================
Date: 01/15/2025          Time: 12:47 PM
Cashier: Emily           Register: 03
========================================

Organic Avocados (3)        3 x $1.99    $5.97
Sourdough Bread                          $4.49
Wild Salmon Fillet 1.2lb    1 x $14.99  $17.99
Baby Spinach Organic 5oz                 $3.99
Greek Yogurt Plain 32oz                  $5.49
Free Range Eggs (dozen)                  $6.79
Almond Milk Unsweetened                  $3.29
Heirloom Tomatoes 1.5lb    1 x $4.99    $7.49
Quinoa Organic 16oz                      $5.99
Dark Chocolate 72% Cacao                 $3.89

----------------------------------------
Subtotal                               $65.38
Member Discount (10%)                  -$6.54
----------------------------------------
Subtotal after Discount                $58.84
Sales Tax (8.625%)                      $5.08
========================================
TOTAL                                  $63.92
========================================

VISA ending in 4821
Auth: 847291
Transaction: WFM-SF-20250115-003847

Points Earned: 64 pts
Total Points: 2,847 pts

Thank you for shopping at Whole Foods!
Visit us at wholefoods.com
========================================
";
        }
    }
}
