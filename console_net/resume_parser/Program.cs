using LMKit.Extraction;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace resume_parser
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
            Console.WriteLine("=== AI Resume Parser ===\n");
            Console.WriteLine("Extract structured candidate profiles from any resume.");
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
            WriteColor("║                      AI RESUME PARSER                         ║", ConsoleColor.Cyan);
            WriteColor("╚═══════════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();
            Console.WriteLine("Parse any resume into a structured candidate profile instantly.");
            Console.WriteLine();
            Console.WriteLine("  sample  - Try with a built-in sample resume");
            Console.WriteLine("  <path>  - Parse a resume file (.txt, .pdf, .docx)");
            Console.WriteLine("  q       - Quit");
            Console.WriteLine();

            while (true)
            {
                WriteColor("Resume> ", ConsoleColor.Green, addNL: false);
                string? input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                    break;

                if (input.Equals("sample", StringComparison.OrdinalIgnoreCase))
                {
                    string sampleResume = GetSampleResume();
                    WriteColor("\n--- Sample Resume ---", ConsoleColor.DarkGray);
                    Console.WriteLine(sampleResume);
                    WriteColor("--- End of Resume ---\n", ConsoleColor.DarkGray);
                    textExtraction.SetContent(sampleResume);
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

                Console.WriteLine("\nExtracting candidate profile...\n");
                var sw = Stopwatch.StartNew();

                try
                {
                    var result = textExtraction.Parse();
                    sw.Stop();

                    WriteColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
                    WriteColor("║                    CANDIDATE PROFILE                          ║", ConsoleColor.Cyan);
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
                new TextExtractionElement("Full Name", ElementType.String, "Full name of the candidate."),
                new TextExtractionElement("Email", ElementType.String, "Email address."),
                new TextExtractionElement("Phone", ElementType.String, "Phone number."),
                new TextExtractionElement("Location", ElementType.String, "City, state, or country of residence."),
                new TextExtractionElement("Professional Summary", ElementType.String,
                    "Brief professional summary or career objective."),

                new TextExtractionElement(
                    "Work Experience",
                    new List<TextExtractionElement>
                    {
                        new("Company", ElementType.String, "Company or organization name."),
                        new("Job Title", ElementType.String, "Job title or role."),
                        new("Period", ElementType.String, "Employment period (e.g. Jan 2020 - Present)."),
                        new("Key Achievements", ElementType.String,
                            "Key achievements and responsibilities, summarized.")
                    },
                    isArray: true,
                    "List of work experience entries, from most recent to oldest."
                ),

                new TextExtractionElement(
                    "Education",
                    new List<TextExtractionElement>
                    {
                        new("Institution", ElementType.String, "School or university name."),
                        new("Degree", ElementType.String, "Degree and field of study."),
                        new("Year", ElementType.String, "Graduation year or study period.")
                    },
                    isArray: true,
                    "List of education entries."
                ),

                new TextExtractionElement("Skills", ElementType.String,
                    "All technical and professional skills, comma-separated."),
                new TextExtractionElement("Certifications", ElementType.String,
                    "Professional certifications, comma-separated. Empty if none."),
                new TextExtractionElement("Languages", ElementType.String,
                    "Languages spoken with proficiency levels, comma-separated.")
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

        private static string GetSampleResume()
        {
            return @"
MARIA RODRIGUEZ
San Francisco, CA | maria.rodriguez@email.com | (415) 555-0192 | linkedin.com/in/mariarodriguez

PROFESSIONAL SUMMARY
Senior Full-Stack Engineer with 8+ years of experience building scalable web applications
and distributed systems. Led teams of 5-12 engineers at high-growth startups. Passionate
about clean architecture, developer experience, and mentoring junior engineers.

EXPERIENCE

Principal Software Engineer | CloudScale Technologies | Jan 2022 - Present
- Architected microservices platform serving 2M+ daily active users with 99.99% uptime
- Led migration from monolith to event-driven architecture, reducing deployment time by 75%
- Mentored team of 8 engineers, resulting in 3 promotions to senior level
- Implemented real-time analytics pipeline processing 500K events/second using Kafka and Flink

Senior Software Engineer | DataFlow Inc. | Mar 2019 - Dec 2021
- Built customer-facing dashboard used by 500+ enterprise clients
- Reduced API response times by 60% through caching strategy and query optimization
- Designed and implemented CI/CD pipeline reducing release cycles from 2 weeks to daily
- Won company hackathon for building an ML-powered anomaly detection system

Software Engineer | WebStart Solutions | Jun 2016 - Feb 2019
- Developed RESTful APIs serving 10M+ requests/day using Node.js and PostgreSQL
- Built real-time collaboration features using WebSockets
- Contributed to open-source React component library (2.5K GitHub stars)

EDUCATION

M.S. Computer Science | Stanford University | 2016
  Specialization: Distributed Systems
  Thesis: ""Optimizing Consensus Protocols for Geo-Distributed Databases""

B.S. Computer Engineering | UC Berkeley | 2014
  Graduated with Honors, Dean's List

SKILLS
Languages: Python, TypeScript, Go, Java, SQL
Frameworks: React, Next.js, FastAPI, Spring Boot
Infrastructure: AWS, Kubernetes, Docker, Terraform, GitHub Actions
Databases: PostgreSQL, Redis, MongoDB, DynamoDB, Elasticsearch
Tools: Kafka, GraphQL, gRPC, Prometheus, Grafana

CERTIFICATIONS
- AWS Solutions Architect Professional (2023)
- Certified Kubernetes Administrator (CKA) (2022)
- Google Cloud Professional Data Engineer (2021)

LANGUAGES
English (Native), Spanish (Native), Portuguese (Conversational), Mandarin (Basic)
";
        }
    }
}
