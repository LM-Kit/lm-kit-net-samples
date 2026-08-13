using LMKit.Data;
using LMKit.Finetuning;
using LMKit.Global;
using LMKit.Inference.Vision;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Globalization;
using System.Text;

// Vision fine-tuning demo: teach a small vision-language model to read
// seven-segment displays. Meters, scales, and panel instruments show values
// on segment displays that generic models misread; this demo renders labeled
// display images, measures reading accuracy on held-out values before and
// after LoRA training, and saves the adapter as GGUF. The same workflow
// applies to any labeled image set: replace the generated images with your
// own photos. Everything runs locally.

Console.OutputEncoding = Encoding.UTF8;

// Optional: LMKit.Licensing.LicenseManager.SetLicenseKey("");

const string ModelId = "qwen3.5:0.8b";
const string Question = "What value does the display show?";

// Small synthetic images need no large pixel budget; Standard keeps the
// vision token cost per sample low while preserving the segment shapes,
// for training and inference alike.
Configuration.DefaultImageDetail = ImageDetail.Standard;

// The training backward kernels are CUDA-accelerated, so prefer CUDA when a
// GPU is present (training falls back to CPU when no CUDA device is available).
Runtime.EnableVulkan = false;

// Deterministic, disjoint train and held-out value sets in 0.0 to 99.9.
var rng = new Random(7);
var pool = new HashSet<string>();
while (pool.Count < 58)
{
    pool.Add((rng.Next(0, 1000) / 10.0).ToString("0.0", CultureInfo.InvariantCulture));
}
string[] values = [.. pool];
string[] trainValues = values[..48];
string[] heldOut = values[48..];

Console.WriteLine($"Loading {ModelId}...");
using LM model = LM.LoadFromModelID(ModelId);

int Evaluate(string phase)
{
    int correct = 0;
    foreach (string value in heldOut)
    {
        using var chat = new MultiTurnConversation(model)
        {
            SamplingMode = new GreedyDecoding(),
            ReasoningLevel = ReasoningLevel.None,
            MaximumCompletionTokens = 24
        };
        var attachment = new Attachment(RenderDisplay(value), $"display-{value}.bmp");
        string reply = chat.Submit(new ChatHistory.Message(Question, attachment)).Completion.Trim();

        bool hit = reply.Contains(value, StringComparison.Ordinal);
        if (hit) correct++;
        Console.WriteLine($"  [{(hit ? "ok " : "err")}] {value,5} <- {(reply.Length > 60 ? reply[..60] + "..." : reply)}");
    }
    Console.WriteLine($"{phase}: displays read correctly {correct}/{heldOut.Length}");
    return correct;
}

Console.WriteLine("\nBefore fine-tuning:");
int before = Evaluate("BASE");

using var finetuning = new LoraFinetuning(model);
finetuning.Parameters.Rank = 8;
finetuning.Parameters.Alpha = 16;
// Image training adapts the attention projections; the Output module is not
// supported when samples carry images.
finetuning.Parameters.TargetModules = LoraTargetModules.Attention;
finetuning.Parameters.Epochs = 3;
finetuning.Parameters.LearningRate = 2e-4f;
finetuning.Parameters.Schedule = LearningRateSchedule.Cosine;
finetuning.Parameters.ValidationSplit = 0;
finetuning.Parameters.Seed = 42;

// One conversation per labeled image, packed into a single history with
// begin-of-conversation markers.
var data = new ChatHistory(model);
for (int i = 0; i < trainValues.Length; i++)
{
    if (i > 0)
    {
        data.AddMessage(AuthorRole.BeginOfNewConversation, string.Empty);
    }
    var attachment = new Attachment(RenderDisplay(trainValues[i]), $"train-{i}.bmp");
    data.AddMessage(new ChatHistory.Message(Question, attachment));
    data.AddMessage(AuthorRole.Assistant, $"The display reads {trainValues[i]}.");
}

int samples = finetuning.AddTrainingData(data);
Console.WriteLine($"\nTraining on {samples} labeled displays (rank {finetuning.Parameters.Rank}, {finetuning.Parameters.Epochs} epochs)...");

finetuning.FinetuningProgress += (s, e) =>
{
    if (!e.IsValidation)
    {
        Console.Write($"\r  epoch {e.Epoch + 1}/{e.TotalEpochs}  step {e.Step}/{e.TotalSteps}  loss {e.Loss:F4}   ");
    }
};

string adapterPath = Path.Combine(AppContext.BaseDirectory, "display-reader.gguf");
finetuning.TrainToAdapter(adapterPath);
Console.WriteLine($"\nAdapter saved: {adapterPath} ({new FileInfo(adapterPath).Length / 1024} KB)");

model.ApplyLoraAdapter(new LoraAdapterSource(adapterPath));

Console.WriteLine("\nAfter fine-tuning:");
int after = Evaluate("TUNED");

Console.WriteLine($"\nHeld-out displays read correctly: {before}/{heldOut.Length} -> {after}/{heldOut.Length}");

// Renders a value like "47.3" as a seven-segment panel: lit green segments
// and a faint ghost of the unlit ones on a dark background, the look of a
// real LCD/LED instrument. 24bpp BMP, no image library needed.
static byte[] RenderDisplay(string value)
{
    const int t = 6, half = 21;                 // segment thickness and vertical segment length
    const int digitW = 24 + 2 * t, digitH = 2 * half + 3 * t;
    const int gap = 10, dotSize = t, margin = 14;

    string[] glyphs = [.. value.Split('.')];
    string intPart = glyphs[0].PadLeft(2);      // blank leading digit below 10, like a real panel
    string fracPart = glyphs.Length > 1 ? glyphs[1] : "0";
    string digits = intPart + fracPart;         // three glyph cells, dot drawn between cells 2 and 3

    int width = margin * 2 + 3 * digitW + 2 * gap + dotSize + gap;
    int height = margin * 2 + digitH;

    int rowBytes = (width * 3 + 3) & ~3;
    byte[] bmp = new byte[54 + rowBytes * height];
    WriteBmpHeader(bmp, width, height, rowBytes);
    FillRect(bmp, rowBytes, height, 0, 0, width, height, 0x0C0E0C);

    // Segment bit layout per digit: a b c d e f g (bit 0 to 6).
    int[] segmentMap = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F];

    int x = margin;
    for (int cell = 0; cell < 3; cell++)
    {
        char c = digits[cell];
        int lit = c == ' ' ? 0 : segmentMap[c - '0'];
        DrawDigit(bmp, rowBytes, height, x, margin, t, half, lit);
        x += digitW + gap;
        if (cell == 1)
        {
            FillRect(bmp, rowBytes, height, x - gap / 2 - dotSize / 2, margin + digitH - dotSize, dotSize, dotSize, 0x3CFF78);
            x += dotSize + gap;
        }
    }
    return bmp;

    static void DrawDigit(byte[] bmp, int rowBytes, int imgH, int x, int y, int t, int half, int lit)
    {
        int len = 24;
        (int X, int Y, int W, int H)[] segments =
        [
            (x + t, y, len, t),                          // a: top
            (x + t + len, y + t, t, half),               // b: top right
            (x + t + len, y + 2 * t + half, t, half),    // c: bottom right
            (x + t, y + 2 * t + 2 * half, len, t),       // d: bottom
            (x, y + 2 * t + half, t, half),              // e: bottom left
            (x, y + t, t, half),                         // f: top left
            (x + t, y + t + half, len, t),               // g: middle
        ];
        for (int s = 0; s < segments.Length; s++)
        {
            int color = (lit & (1 << s)) != 0 ? 0x3CFF78 : 0x1A201A;
            FillRect(bmp, rowBytes, imgH, segments[s].X, segments[s].Y, segments[s].W, segments[s].H, color);
        }
    }

    static void FillRect(byte[] bmp, int rowBytes, int imgH, int x, int y, int w, int h, int rgb)
    {
        byte r = (byte)(rgb >> 16), g = (byte)(rgb >> 8), b = (byte)rgb;
        for (int py = y; py < y + h; py++)
        {
            int row = 54 + (imgH - 1 - py) * rowBytes;   // BMP rows are bottom-up
            for (int px = x; px < x + w; px++)
            {
                int i = row + px * 3;
                bmp[i] = b;
                bmp[i + 1] = g;
                bmp[i + 2] = r;
            }
        }
    }

    static void WriteBmpHeader(byte[] bmp, int width, int height, int rowBytes)
    {
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        WriteInt(bmp, 2, bmp.Length);
        WriteInt(bmp, 10, 54);
        WriteInt(bmp, 14, 40);
        WriteInt(bmp, 18, width);
        WriteInt(bmp, 22, height);
        bmp[26] = 1;
        bmp[28] = 24;
        WriteInt(bmp, 34, rowBytes * height);
        WriteInt(bmp, 38, 2835);
        WriteInt(bmp, 42, 2835);

        static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
