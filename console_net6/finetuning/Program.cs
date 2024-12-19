using finetuning.Experiments;

namespace finetuning
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            // Uncomment the finetuning experiment you want to run:

            SentimentAnalysisFinetuning.RunTraining();
            //SarcasmDetectionFinetuning.RunTraining();
            //ChemistryAssistantFinetuning.RunTraining();
        }
    }
}