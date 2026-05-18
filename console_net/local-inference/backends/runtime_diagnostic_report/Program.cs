using LMKit.Global;
using LMKit.Hardware.Gpu;
using LMKit.Model;
using System.Text;

namespace backends
{
    internal class Program
    {
        private static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== LM-Kit Hardware Backends Inspector ===\n");

            Runtime.Initialize();

            Console.WriteLine($"LM-Kit runtime version : {Runtime.Version}");
            Console.WriteLine($"Selected backend       : {Runtime.Backend}");
            Console.WriteLine($"Has GPU support        : {Runtime.HasGpuSupport}");
            Console.WriteLine($"CUDA enabled           : {Runtime.EnableCuda}");
            Console.WriteLine($"Vulkan enabled         : {Runtime.EnableVulkan}");
            Console.WriteLine($"Backend directory      : {Runtime.BackendDirectory}");
            Console.WriteLine();

            IReadOnlyList<GpuDeviceInfo> gpus = GpuDeviceInfo.Devices;
            if (gpus.Count == 0)
            {
                Console.WriteLine("No GPU device visible. Inference will run on CPU.");
            }
            else
            {
                Console.WriteLine($"Detected {gpus.Count} GPU device(s):\n");
                Console.WriteLine($"  # | {"Device",-32} | {"Total VRAM",12} | {"Free VRAM",12}");
                Console.WriteLine(new string('-', 80));
                foreach (GpuDeviceInfo g in gpus)
                {
                    Console.WriteLine(
                        $"  {g.DeviceNumber} | {Truncate(g.DeviceName, 32),-32} | " +
                        $"{FormatBytes((long)g.TotalMemorySize),12} | " +
                        $"{FormatBytes((long)g.FreeMemorySize),12}");
                }
                Console.WriteLine();
                GpuDeviceInfo bestByVram = gpus.OrderByDescending(g => g.FreeMemorySize).First();
                Console.WriteLine($"GPU with the most free VRAM: #{bestByVram.DeviceNumber} ({bestByVram.DeviceName}).");
            }
            Console.WriteLine();

            Console.Write("Load a small model and observe layer placement? (Y/n) ");
            string ans = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (ans == "n") { return; }

            Console.WriteLine();
            Console.WriteLine("Loading gemma3:270m (auto device configuration)...");
            using (LM model = LM.LoadFromModelID(
                "gemma3:270m",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress))
            {
                Console.WriteLine();
                Console.WriteLine($"  Layers in model       : {model.LayerCount}");
                Console.WriteLine($"  Layers offloaded GPU  : {model.GpuLayerCount}");
                Console.WriteLine($"  Layers on CPU         : {model.LayerCount - model.GpuLayerCount}");
            }

            if (!Runtime.HasGpuSupport)
            {
                Console.WriteLine();
                Console.WriteLine("Skipping CPU-pinned reload (no GPU support to contrast against).");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Reloading with CPU-only configuration (GpuLayerCount = 0)...");
            ModelCard card = ModelCard.GetPredefinedModelCardByModelID("gemma3:270m");
            LM.DeviceConfiguration cpuOnly = new() { GpuLayerCount = 0 };
            using (LM cpu = new(card.ModelUri, deviceConfiguration: cpuOnly, loadingProgress: OnLoadProgress))
            {
                Console.WriteLine();
                Console.WriteLine($"  CPU reload          : layers offloaded GPU = {cpu.GpuLayerCount}");
            }
        }

        static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static string FormatBytes(long bytes)
        {
            if (bytes <= 0) { return "?"; }
            string[] units = ["B", "KB", "MB", "GB"];
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return $"{v:F1} {units[u]}";
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
