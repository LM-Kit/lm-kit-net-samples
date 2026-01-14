namespace batch_pii_extraction
{
    internal sealed partial class Stats
    {
        public readonly struct Snapshot
        {
            public long Documents { get; }
            public long Pages { get; }
            public TimeSpan Elapsed { get; }
            public TimeSpan AvgPerDocument { get; }
            public TimeSpan AvgPerPage { get; }
            public double DocsPerSec { get; }
            public double PagesPerSec { get; }

            public Snapshot(
                long documents, long pages, TimeSpan elapsed,
                TimeSpan avgPerDocument, TimeSpan avgPerPage,
                double docsPerSec, double pagesPerSec)
            {
                Documents = documents;
                Pages = pages;
                Elapsed = elapsed;
                AvgPerDocument = avgPerDocument;
                AvgPerPage = avgPerPage;
                DocsPerSec = docsPerSec;
                PagesPerSec = pagesPerSec;
            }
        }
    }
}