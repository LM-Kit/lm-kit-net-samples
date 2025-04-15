namespace finetuning
{
    internal static class SystemUtils
    {
        public static float GetTotalMemoryGB()
        {
            return (float)Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 2);
        }
    }
}