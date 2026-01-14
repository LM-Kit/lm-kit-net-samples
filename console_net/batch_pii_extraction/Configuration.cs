using LMKit.Inference;
using LMKit.Integrations.Tesseract;
using LMKit.Model;
using LMKit.TextAnalysis;

namespace batch_pii_extraction
{
    internal static class Configuration
    {
        public static bool IncludeOtherType = false;

        public static InferenceModality PreferredInferenceModality = InferenceModality.Text; //highly suggested if the final objective is to redact identified areas.

        public static int GetMaxThreadCount(LM model)
        {
            var defaultDev = LMKit.Hardware.Gpu.GpuDeviceInfo.GetDeviceFromNumber(0);
            int maxThreadCount;

            if (defaultDev != null && defaultDev.TotalMemorySize > 8L * 1024 * 1024 * 1024)
            {
                maxThreadCount = model.ParameterCount < 10_000_000_000 ? 3 : 2;
            }
            else
            {
                maxThreadCount = 1;
            }

            return maxThreadCount;
        }

        //complete catalog: https://docs.lm-kit.com/lm-kit-net/guides/getting-started/model-catalog.html
        public static string ModelId = "lmkit-tasks:4b-preview";

        public static PiiExtraction SetupEngine(LM model)
        {
            PiiExtraction engine = new(
                model,
                includeOtherType: Configuration.IncludeOtherType)
            {
                PreferredInferenceModality = PreferredInferenceModality,
                OcrEngine = new TesseractOcr(), //Custom OCR can be used by implementing the MyCustomOcr class
                Guidance = "" //example of guidance: "consider swift code as private information";
            };

            //example of new input entity definition
            //engine.PiiEntityDefinitions.Add(
            //    new PiiExtraction.PiiEntityDefinition("swift code"));

            //example of external OCR usage
            //engine.OcrEngine = new TesseractOcrEngine();
            //engine.OcrEngine = new MyCustomOcr();
            //engine.OcrEngine = new LMKit.Integrations.AWS.Ocr.Textract.TextractOcr(
            //   "awsAccessKeyId", 
            //   "awsSecretAccessKey",
            //   LMKit.Integrations.AWS.AWSRegion.EUCentral1);

            return engine;
        }
    }
}