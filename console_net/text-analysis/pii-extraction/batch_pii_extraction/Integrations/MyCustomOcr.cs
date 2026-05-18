using LMKit.Extraction.Ocr;

namespace pii_extraction.Integrations
{
    public class MyCustomOcr : OcrEngine
    {
        public override Task<OcrResult> RunAsync(
            OcrParameters ocrParameters,
            CancellationToken cancellationToken = default)
        {
            //todo: ocrParameters -> OCR -> OcrResult
            throw new NotImplementedException();
        }
    }
}