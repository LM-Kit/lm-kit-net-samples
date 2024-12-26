using LMKit.Model;
using LMKit.Quantization;

namespace text_rewriter
{
    internal class quantizer
    {

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            string modelPath = "";

            Console.Clear();

            //getting valid input model
            while (string.IsNullOrWhiteSpace(modelPath))
            {
                Console.Write("Please enter the path to the model for quantization (recommended format: fp16):\n\n>");
                string input = Console.ReadLine().Trim().Trim('"');

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (File.Exists(input))
                    {
                        if (LM.ValidateFormat(input))
                        {
                            modelPath = input;
                        }
                        else
                        {
                            Console.WriteLine("Unsupported file format. Expected is a GGUF model.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("The provided path is invalid.");
                    }
                }
            }

            //getting quantization format
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

            Console.Write("\n\n>");
            string format = Console.ReadLine().Trim().Trim('"').Trim('\'').ToUpperInvariant();

            while (!IsValidQuantizationFormat(format))
            {
                Console.Write("The provided quantization is invalid.\n\n>");
            }

            DoQuantization(modelPath, format);

            Console.Write("The operation ended. Hit any key to continue");
            _ = Console.ReadKey();
        }

        private static bool IsValidQuantizationFormat(string format)
        {
            return (new string[] { "Q2_K", "Q3_K_S", "Q3_K_M", "Q3_K_L", "Q4_K_S", "Q4_K_M", "Q5_K_S", "Q5_K_M", "Q6_K", "Q8_0", "ALL" }).Contains(format);

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
            {
                dstFileName = dstFileName.Substring(0, dstFileName.Length - 4);
            }

            string dstModelPath = Path.Combine(Path.GetDirectoryName(modelPath), dstFileName + "-" + format + ".gguf");

            LM.Precision quantizationFormat;

            switch (format)
            {
                case "Q2_K":
                    quantizationFormat = LM.Precision.MOSTLY_Q2_K;
                    break;
                case "Q3_K_S":
                    quantizationFormat = LM.Precision.MOSTLY_Q3_K_S;
                    break;
                case "Q3_K_M":
                    quantizationFormat = LM.Precision.MOSTLY_Q3_K_M;
                    break;
                case "Q3_K_L":
                    quantizationFormat = LM.Precision.MOSTLY_Q3_K_L;
                    break;
                case "Q4_K_S":
                    quantizationFormat = LM.Precision.MOSTLY_Q4_K_S;
                    break;
                case "Q4_K_M":
                    quantizationFormat = LM.Precision.MOSTLY_Q4_K_M;
                    break;
                case "Q5_K_S":
                    quantizationFormat = LM.Precision.MOSTLY_Q5_K_S;
                    break;
                case "Q5_K_M":
                    quantizationFormat = LM.Precision.MOSTLY_Q5_K_M;
                    break;
                case "Q6_K":
                    quantizationFormat = LM.Precision.MOSTLY_Q6_K;
                    break;
                case "Q8_0":
                    quantizationFormat = LM.Precision.MOSTLY_Q8_0;
                    break;
                default:
                    throw new InvalidOperationException("unhandled quantization format");
            }

            Console.WriteLine($"Generating {dstModelPath} with precision {quantizationFormat}...");
            Quantizer quantizer = new Quantizer(modelPath);

            quantizer.Quantize(dstModelPath, quantizationFormat);
        }
    }
}