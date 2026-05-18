using LMKit.Model;
using System.Text;

namespace quantizer
{
    internal class Quantizer
    {
        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();

            string modelPath = "";

            while (string.IsNullOrWhiteSpace(modelPath))
            {
                Console.Write("Please enter the path to the model for quantization (recommended format: fp16):\n\n> ");
                string input = Console.ReadLine()?.Trim().Trim('"') ?? "";

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (File.Exists(input))
                    {
                        if (LM.ValidateFormat(input))
                            modelPath = input;
                        else
                            Console.WriteLine("Unsupported file format. Expected is a GGUF model.");
                    }
                    else
                    {
                        Console.WriteLine("The provided path is invalid.");
                    }
                }
            }

            Console.WriteLine("\n\nPlease enter the output model format (e.g., 'Q4_K_M')");
            Console.Write("\n Accepted formats:\n -----------------\n");
            Console.WriteLine(@"
 Q2_K    | smallest, significant quality loss - not recommended for most purposes
 Q3_K_S  | very small, high quality loss
 Q3_K_M  | very small, high quality loss
 Q3_K_L  | small, substantial quality loss
 Q4_K_S  | small, greater quality loss
 Q4_K_M  | medium, balanced quality - recommended
 Q5_K_S  | large, low quality loss - recommended
 Q5_K_M  | large, very low quality loss - recommended
 Q6_K    | very large, extremely low quality loss
 Q8_0    | very large, extremely low quality loss - not recommended
 ALL     | generate a version for all formats
");

            Console.Write("\n\n> ");
            string format = Console.ReadLine()?.Trim().Trim('"').Trim('\'').ToUpperInvariant() ?? "";

            while (!IsValidQuantizationFormat(format))
            {
                Console.Write("The provided quantization format is invalid.\n\n> ");
                format = Console.ReadLine()?.Trim().Trim('"').Trim('\'').ToUpperInvariant() ?? "";
            }

            DoQuantization(modelPath, format);

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static bool IsValidQuantizationFormat(string format)
        {
            return format is "Q2_K" or "Q3_K_S" or "Q3_K_M" or "Q3_K_L" or "Q4_K_S"
                or "Q4_K_M" or "Q5_K_S" or "Q5_K_M" or "Q6_K" or "Q8_0" or "ALL";
        }

        private static void DoQuantization(string modelPath, string format)
        {
            if (format == "ALL")
            {
                DoQuantization(modelPath, "Q2_K");
                DoQuantization(modelPath, "Q3_K_S");
                DoQuantization(modelPath, "Q3_K_M");
                DoQuantization(modelPath, "Q3_K_L");
                DoQuantization(modelPath, "Q4_K_S");
                DoQuantization(modelPath, "Q4_K_M");
                DoQuantization(modelPath, "Q5_K_S");
                DoQuantization(modelPath, "Q5_K_M");
                DoQuantization(modelPath, "Q6_K");
                DoQuantization(modelPath, "Q8_0");
                return;
            }

            string dstFileName = Path.GetFileNameWithoutExtension(modelPath);

            if (dstFileName.ToUpper().EndsWith("-F16") || dstFileName.ToUpper().EndsWith("-F32"))
                dstFileName = dstFileName.Substring(0, dstFileName.Length - 4);

            string dstModelPath = Path.Combine(Path.GetDirectoryName(modelPath)!, dstFileName + "-" + format + ".gguf");

            LM.Precision quantizationFormat = format switch
            {
                "Q2_K" => LM.Precision.MOSTLY_Q2_K,
                "Q3_K_S" => LM.Precision.MOSTLY_Q3_K_S,
                "Q3_K_M" => LM.Precision.MOSTLY_Q3_K_M,
                "Q3_K_L" => LM.Precision.MOSTLY_Q3_K_L,
                "Q4_K_S" => LM.Precision.MOSTLY_Q4_K_S,
                "Q4_K_M" => LM.Precision.MOSTLY_Q4_K_M,
                "Q5_K_S" => LM.Precision.MOSTLY_Q5_K_S,
                "Q5_K_M" => LM.Precision.MOSTLY_Q5_K_M,
                "Q6_K" => LM.Precision.MOSTLY_Q6_K,
                "Q8_0" => LM.Precision.MOSTLY_Q8_0,
                _ => throw new InvalidOperationException("unhandled quantization format")
            };

            Console.WriteLine($"Generating {dstModelPath} with precision {quantizationFormat}...");
            LMKit.Quantization.Quantizer quantizer = new(modelPath);
            quantizer.Quantize(dstModelPath, quantizationFormat);
        }
    }
}
