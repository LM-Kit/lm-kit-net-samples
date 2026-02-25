using LMKit.Extraction;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace contract_key_terms_extractor
{
    internal class Program
    {
        private static bool _isDownloading;

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== AI Contract Key Terms Extractor ===\n");
            Console.WriteLine("Extract key clauses and terms from any contract or agreement.");
            Console.WriteLine("All processing runs locally on your hardware.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen-3.5 27B     (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string inputStr = Console.ReadLine() ?? string.Empty;
            LM model = LoadModel(inputStr);

            Console.Clear();

            var textExtraction = new TextExtraction(model);
            textExtraction.Elements = CreateElements();

            WriteColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteColor("║              AI CONTRACT KEY TERMS EXTRACTOR                   ║", ConsoleColor.Cyan);
            WriteColor("╚═══════════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();
            Console.WriteLine("Extract key terms from any contract, NDA, or legal agreement.");
            Console.WriteLine();
            Console.WriteLine("  sample  - Try with a built-in sample contract");
            Console.WriteLine("  <path>  - Analyze a contract file (.txt, .pdf, .docx)");
            Console.WriteLine("  q       - Quit");
            Console.WriteLine();

            while (true)
            {
                WriteColor("Contract> ", ConsoleColor.Green, addNL: false);
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                    break;

                if (input.Equals("sample", StringComparison.OrdinalIgnoreCase))
                {
                    string sampleContract = GetSampleContract();
                    WriteColor("\n--- Sample Contract ---", ConsoleColor.DarkGray);
                    Console.WriteLine(sampleContract);
                    WriteColor("--- End of Contract ---\n", ConsoleColor.DarkGray);
                    textExtraction.SetContent(sampleContract);
                }
                else
                {
                    string filePath = input.Trim('"');
                    if (!File.Exists(filePath))
                    {
                        WriteColor($"File not found: {filePath}\n", ConsoleColor.Red);
                        continue;
                    }

                    WriteColor($"\nLoading: {Path.GetFileName(filePath)}", ConsoleColor.DarkGray);
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();

                    if (ext == ".txt")
                        textExtraction.SetContent(File.ReadAllText(filePath));
                    else
                        textExtraction.SetContent(new LMKit.Data.Attachment(filePath));
                }

                Console.WriteLine("\nExtracting key terms...\n");
                var sw = Stopwatch.StartNew();

                try
                {
                    var result = textExtraction.Parse();
                    sw.Stop();

                    WriteColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
                    WriteColor("║                   KEY CONTRACT TERMS                          ║", ConsoleColor.Cyan);
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
                new TextExtractionElement("Contract Type", ElementType.String,
                    "Type of contract (e.g. NDA, Service Agreement, Employment Contract, Lease, SaaS Agreement)."),
                new TextExtractionElement("Contract Title", ElementType.String,
                    "Official title of the contract document."),

                new TextExtractionElement(
                    "Parties",
                    new List<TextExtractionElement>
                    {
                        new("Name", ElementType.String, "Full legal name of the party."),
                        new("Role", ElementType.String, "Role in the contract (e.g. Client, Provider, Licensor, Licensee)."),
                        new("Address", ElementType.String, "Registered address if mentioned.")
                    },
                    isArray: true,
                    "Parties involved in the contract."
                ),

                new TextExtractionElement("Effective Date", ElementType.Date, "Date when the contract becomes effective."),
                new TextExtractionElement("Expiration Date", ElementType.Date,
                    "End date or expiration date. If perpetual, state 'Perpetual'."),
                new TextExtractionElement("Contract Value", ElementType.String,
                    "Total monetary value, fee, or compensation specified in the contract."),
                new TextExtractionElement("Payment Terms", ElementType.String,
                    "Payment schedule, method, and conditions (e.g. Net 30, monthly, upon delivery)."),
                new TextExtractionElement("Termination Clause", ElementType.String,
                    "Conditions under which the contract can be terminated and notice period required."),
                new TextExtractionElement("Confidentiality", ElementType.String,
                    "Summary of confidentiality or non-disclosure obligations."),
                new TextExtractionElement("Liability Limitation", ElementType.String,
                    "Liability cap or limitation of damages clause."),
                new TextExtractionElement("Intellectual Property", ElementType.String,
                    "IP ownership, licensing rights, or work-for-hire provisions."),
                new TextExtractionElement("Governing Law", ElementType.String,
                    "Jurisdiction and governing law for disputes."),
                new TextExtractionElement("Key Obligations", ElementType.String,
                    "Main obligations and deliverables for each party, summarized."),
                new TextExtractionElement("Renewal Terms", ElementType.String,
                    "Auto-renewal conditions, if any. 'None' if not mentioned."),
                new TextExtractionElement("Risk Flags", ElementType.String,
                    "Any unusual or potentially risky clauses (non-compete, exclusivity, unlimited liability, auto-renewal, penalty clauses).")
            };
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3:8b";
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

        private static string GetSampleContract()
        {
            return @"
MASTER SERVICES AGREEMENT

This Master Services Agreement (""Agreement"") is entered into as of March 1, 2025
(""Effective Date"") by and between:

NovaTech Solutions Inc., a Delaware corporation with principal offices at
1200 Innovation Drive, Suite 400, Austin, TX 78701 (""Provider"")

AND

Meridian Global Partners LLC, a New York limited liability company with principal
offices at 500 Park Avenue, 12th Floor, New York, NY 10022 (""Client"")

(each a ""Party"" and collectively the ""Parties"")

1. SCOPE OF SERVICES
Provider shall deliver software development, cloud infrastructure consulting,
and technical support services as described in individual Statements of Work (SOW)
executed under this Agreement. Each SOW shall detail specific deliverables,
timelines, acceptance criteria, and fees.

2. TERM AND TERMINATION
This Agreement shall commence on the Effective Date and continue for a period
of twenty-four (24) months, expiring on February 28, 2027 (""Initial Term"").
The Agreement shall automatically renew for successive twelve (12) month periods
unless either Party provides written notice of non-renewal at least ninety (90)
days prior to the end of the then-current term.

Either Party may terminate this Agreement for cause if the other Party materially
breaches any provision and fails to cure such breach within thirty (30) days of
receiving written notice. Client may terminate for convenience upon sixty (60)
days' written notice, subject to payment for all services rendered and
non-cancelable commitments.

3. FEES AND PAYMENT
Client shall pay Provider a base monthly retainer of $45,000 USD for up to
160 hours of professional services per month. Hours exceeding the monthly
allocation shall be billed at $350 USD per hour. Payment terms are Net 30
from date of invoice. Late payments shall bear interest at 1.5% per month.

Total estimated contract value for the Initial Term: $1,080,000 USD.

4. CONFIDENTIALITY
Each Party agrees to hold in strict confidence all Confidential Information
received from the other Party. ""Confidential Information"" includes business
plans, technical data, customer lists, financial information, source code,
and any information marked as confidential. Confidentiality obligations shall
survive termination of this Agreement for a period of five (5) years.

5. INTELLECTUAL PROPERTY
All work product, inventions, and deliverables created by Provider specifically
for Client under this Agreement (""Work Product"") shall be owned by Client upon
full payment. Provider retains ownership of all pre-existing tools, frameworks,
and methodologies (""Provider IP""). Provider grants Client a perpetual,
non-exclusive, royalty-free license to use Provider IP embedded in the Work
Product solely in connection with the Work Product.

6. LIMITATION OF LIABILITY
IN NO EVENT SHALL EITHER PARTY BE LIABLE FOR INDIRECT, INCIDENTAL, SPECIAL,
CONSEQUENTIAL, OR PUNITIVE DAMAGES. PROVIDER'S TOTAL AGGREGATE LIABILITY UNDER
THIS AGREEMENT SHALL NOT EXCEED THE TOTAL FEES PAID BY CLIENT IN THE TWELVE (12)
MONTHS PRECEDING THE CLAIM. THIS LIMITATION SHALL NOT APPLY TO BREACHES OF
CONFIDENTIALITY OR INTELLECTUAL PROPERTY INFRINGEMENT.

7. NON-SOLICITATION
During the term and for twelve (12) months thereafter, neither Party shall
directly solicit for employment any employee of the other Party who was involved
in performing services under this Agreement without prior written consent.

8. GOVERNING LAW AND DISPUTE RESOLUTION
This Agreement shall be governed by the laws of the State of New York without
regard to conflict of laws principles. Any dispute arising under this Agreement
shall first be submitted to good-faith mediation. If mediation fails, disputes
shall be resolved by binding arbitration under the rules of the American
Arbitration Association in New York, NY.

9. INSURANCE
Provider shall maintain commercial general liability insurance with minimum
coverage of $2,000,000 per occurrence and professional liability (errors and
omissions) insurance with minimum coverage of $5,000,000 per claim throughout
the term of this Agreement.

IN WITNESS WHEREOF, the Parties have executed this Agreement as of the date
first written above.

NovaTech Solutions Inc.                Meridian Global Partners LLC
By: James Chen, CEO                    By: Victoria Blackwell, Managing Partner
Date: March 1, 2025                    Date: March 1, 2025
";
        }
    }
}
