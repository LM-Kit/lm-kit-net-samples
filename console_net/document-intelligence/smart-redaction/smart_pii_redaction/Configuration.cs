using LMKit.Extraction.Ocr;
using LMKit.Inference;
using LMKit.Model;
using LMKit.TextAnalysis;

namespace smart_pii_redaction
{
    /// <summary>
    /// Central place to configure the PII detection engine used for smart redaction.
    /// </summary>
    internal static class Configuration
    {
        /// <summary>
        /// Builds a <see cref="PiiExtraction"/> engine tuned for redaction.
        /// </summary>
        public static PiiExtraction CreateExtractor(LM model)
        {
            var extractor = new PiiExtraction(model)
            {
                // Text modality resolves every detected entity to the exact glyph
                // positions on the page (PiiExtractedEntity.Occurrences). Those
                // bounding boxes are what the redactor uses to place each mark, so
                // Text is the recommended modality when the goal is redaction.
                PreferredInferenceModality = InferenceModality.Text,

                // OCR recovers a positioned text layer from scanned pages, so the
                // occurrence bounding boxes are available for image-based documents too.
                OcrEngine = new LMKitOcr(),

                // Optional domain guidance to raise accuracy, e.g.
                // Guidance = "Treat internal case numbers as private information.",
            };

            // Add a custom PII category the standard set does not cover, e.g.
            //   extractor.PiiEntityDefinitions.Add(
            //       new PiiExtraction.PiiEntityDefinition("SWIFT code"));

            return extractor;
        }
    }
}
