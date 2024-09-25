using finetuning.Experiments;

namespace finetuning
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(""); //set an optional license key here if available.

            // Uncomment the finetuning experiment you want to run:

            //SentimentAnalysisFinetuning.RunTraining();
            SarcasmDetectionFinetuning.RunTraining();
            //ChemistryAssistantFinetuning.RunTraining();
        }
    }
}