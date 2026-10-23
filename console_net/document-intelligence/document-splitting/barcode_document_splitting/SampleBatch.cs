using LMKit.Graphics;
using LMKit.Media.Image;
using System.Text;

namespace barcode_document_splitting
{
    /// <summary>
    /// Builds a small scanned batch as a multi-page TIFF: QR separator sheets (encoded with
    /// <see cref="QrCode"/>), the blank back of each sheet, and content pages drawn as text-like
    /// lines. Pages are painted in a grayscale buffer and decoded by <see cref="ImageBuffer"/>.
    /// </summary>
    internal static class SampleBatch
    {
        private const int Width = 850, Height = 1100, Dpi = 100; // US Letter at 100 DPI

        /// <summary>Writes the sample batch to a temporary folder and returns its path.</summary>
        public static string Create()
        {
            // 'S:<value>' is a separator sheet, 'B' its blank back, 'C' a content page.
            string[] layout =
            {
                "S:INV-1001", "B", "C", "C",
                "S:PO-2001", "B", "C",
                "S:INV-1002", "B", "C", "C", "C",
                "S:INV-1001", "B", "C"
            };

            string directory = Path.Combine(Path.GetTempPath(), "lmkit_barcode_split_sample");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "scanned_batch.tif");

            var pages = new List<ImageBuffer>();
            try
            {
                for (int i = 0; i < layout.Length; i++)
                {
                    byte[] gray = layout[i] switch
                    {
                        "B" => NewPage(),
                        "C" => ContentPage(seed: i),
                        string sheet => SeparatorSheet(sheet.Substring(2))
                    };

                    ImageBuffer page = ImageBuffer.LoadAsRGB(ToPgm(gray));
                    page.SetResolution(Dpi);
                    pages.Add(page);
                }

                if (!ImageBuffer.SaveAsMultipageTiff(path, pages.ToArray()))
                {
                    throw new IOException($"The sample batch could not be written to '{path}'.");
                }
            }
            finally
            {
                pages.ForEach(p => p.Dispose());
            }

            return path;
        }

        private static byte[] NewPage()
        {
            byte[] pixels = new byte[Width * Height];
            Array.Fill(pixels, (byte)255);
            return pixels;
        }

        private static byte[] SeparatorSheet(string value)
        {
            byte[] pixels = NewPage();
            QrCode code = QrCode.Encode(value, QrCode.ErrorCorrection.Medium);

            const int moduleSize = 12;
            int symbolSize = code.Size * moduleSize;
            int left = (Width - symbolSize) / 2, top = 250;

            for (int y = 0; y < code.Size; y++)
            {
                for (int x = 0; x < code.Size; x++)
                {
                    if (code[x, y])
                    {
                        FillRect(pixels, left + x * moduleSize, top + y * moduleSize, moduleSize, moduleSize, 0);
                    }
                }
            }

            // A heavy frame marks the sheet for the human operator.
            FillRect(pixels, 60, 60, Width - 120, 24, 0);
            FillRect(pixels, 60, Height - 84, Width - 120, 24, 0);
            return pixels;
        }

        private static byte[] ContentPage(int seed)
        {
            byte[] pixels = NewPage();
            var random = new Random(seed);

            FillRect(pixels, 80, 80, 360, 40, 40);          // letterhead
            for (int y = 200; y < 960; y += 30)             // lines of text
            {
                FillRect(pixels, 80, y, 400 + random.Next(290), 10, 70);
            }

            FillRect(pixels, 620, 1010, 150, 12, 120);      // footer
            return pixels;
        }

        private static void FillRect(byte[] pixels, int left, int top, int width, int height, byte value)
        {
            for (int y = top; y < top + height; y++)
            {
                Array.Fill(pixels, value, y * Width + left, width);
            }
        }

        /// <summary>Wraps 8-bit grayscale pixels in a binary PGM (P5) image.</summary>
        private static byte[] ToPgm(byte[] pixels)
        {
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{Width} {Height}\n255\n");
            byte[] image = new byte[header.Length + pixels.Length];
            header.CopyTo(image, 0);
            pixels.CopyTo(image, header.Length);
            return image;
        }
    }
}
