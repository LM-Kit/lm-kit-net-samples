using LMKit.Data;
using System.Diagnostics;

namespace batch_pii_extraction
{
    internal sealed partial class Stats
    {
        private long _totalDocuments;
        private long _totalPages;
        private long _totalProcessingTicks;
        private readonly Stopwatch _wallClock;

        public Stats()
        {
            _wallClock = Stopwatch.StartNew();
        }

        public long TotalDocuments => Interlocked.Read(ref _totalDocuments);
        public long TotalPages => Interlocked.Read(ref _totalPages);
        public TimeSpan TotalElapsed => _wallClock.Elapsed;

        public TimeSpan AverageProcessingTimePerDocument =>
            TotalDocuments == 0 ? TimeSpan.Zero
                                : TimeSpan.FromTicks(Interlocked.Read(ref _totalProcessingTicks) / TotalDocuments);

        public TimeSpan AverageProcessingTimePerPage =>
            TotalPages == 0 ? TimeSpan.Zero
                            : TimeSpan.FromTicks(Interlocked.Read(ref _totalProcessingTicks) / TotalPages);

        public double DocsPerSecond =>
            TotalElapsed.TotalSeconds > 0 ? TotalDocuments / TotalElapsed.TotalSeconds : 0d;

        public double PagesPerSecond =>
            TotalElapsed.TotalSeconds > 0 ? TotalPages / TotalElapsed.TotalSeconds : 0d;

        public void RecordDocument(int pageCount, TimeSpan processingTime)
        {
            Interlocked.Increment(ref _totalDocuments);
            Interlocked.Add(ref _totalPages, pageCount);
            Interlocked.Add(ref _totalProcessingTicks, processingTime.Ticks);
        }

        public void RecordDocument(Attachment attachment, TimeSpan processingTime) =>
            RecordDocument(attachment?.PageCount ?? 0, processingTime);

        public void Stop() => _wallClock.Stop();

        public Snapshot TakeSnapshot() => new(
            TotalDocuments, TotalPages, TotalElapsed,
            AverageProcessingTimePerDocument, AverageProcessingTimePerPage,
            DocsPerSecond, PagesPerSecond);
    }
}