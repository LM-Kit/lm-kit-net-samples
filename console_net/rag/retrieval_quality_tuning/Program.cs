using LMKit.Data;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.Retrieval.Bm25;
using System.Diagnostics;
using System.Text;

namespace retrieval_quality_tuning
{
    internal class Program
    {
        const string EmbeddingModelId = "embeddinggemma-300m";

        static bool _isDownloading;

        // Current retrieval configuration
        static int _topK = 5;
        static float _minScore = 0.3f;
        static float _mmrLambda = 1.0f;
        static int _contextWindow = 0;
        static string _strategyName = "vector";
        static bool _rerankEnabled = false;
        static float _rerankAlpha = 0.5f;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintHeader();

            // ── Step 1: Load embedding model ────────────────────────────────────

            PrintSection("Loading Embedding Model");

            LM embeddingModel = LM.LoadFromModelID(
                EmbeddingModelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            PrintStatus($"Embedding model loaded ({EmbeddingModelId})", ConsoleColor.Green);
            Console.WriteLine();

            // ── Step 2: Build the knowledge base ────────────────────────────────

            PrintSection("Building Knowledge Base");

            var ragEngine = new RagEngine(embeddingModel);
            var knowledgeBase = GetSampleKnowledge();

            var sw = Stopwatch.StartNew();
            int totalChunks = 0;

            foreach (var (topic, content) in knowledgeBase)
            {
                ragEngine.ImportText(
                    content,
                    new TextChunking() { MaxChunkSize = 300 },
                    dataSourceIdentifier: "nebuladb-docs",
                    sectionIdentifier: topic);

                var section = ragEngine.DataSources[0].GetSectionByIdentifier(topic);
                int chunkCount = section?.Partitions.Count ?? 0;
                totalChunks += chunkCount;

                PrintStatus($"  Indexed \"{topic}\" ({chunkCount} chunks)", ConsoleColor.DarkGray);
            }

            sw.Stop();
            PrintStatus(
                $"Knowledge base ready: {knowledgeBase.Count} topics, {totalChunks} chunks in {sw.Elapsed.TotalSeconds:F1}s",
                ConsoleColor.Green);
            Console.WriteLine();

            // ── Step 3: Optional reranking model ────────────────────────────────

            PrintSection("Reranking Model (Optional)");
            PrintStatus("Reranking uses a second embedding pass to re-score initial results.", ConsoleColor.DarkGray);
            PrintStatus("You can enable it later with /rerank on", ConsoleColor.DarkGray);
            Console.WriteLine();

            // ── Step 4: Interactive retrieval loop ──────────────────────────────

            PrintSection("Interactive Retrieval Explorer");
            PrintCommands();
            PrintDivider();
            PrintStatus("Ready! The knowledge base contains fictional NebulaDB documentation.", ConsoleColor.Green);
            PrintStatus("Try queries like:", ConsoleColor.DarkGray);
            PrintStatus("  \"how do I handle connection failures gracefully\"  (semantic, vector excels)", ConsoleColor.DarkGray);
            PrintStatus("  \"NDB-4012 timeout error\"                          (keyword, BM25 excels)", ConsoleColor.DarkGray);
            PrintStatus("  \"configure replication for high availability\"      (both, hybrid excels)", ConsoleColor.DarkGray);
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  Query: ");
                Console.ResetColor();

                string? line = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                string input = line.Trim();

                // Handle commands
                if (input.StartsWith('/'))
                {
                    HandleCommand(input, ragEngine, embeddingModel);
                    continue;
                }

                // Run retrieval with current configuration
                ApplyStrategy(ragEngine);
                RunRetrieval(ragEngine, input);
            }

            Console.WriteLine();
            PrintDivider();
            PrintStatus("Demo ended. Press any key to exit.", ConsoleColor.DarkGray);
            Console.ReadKey(true);
        }

        // ─── Retrieval Execution ───────────────────────────────────────────────

        static void RunRetrieval(RagEngine ragEngine, string query)
        {
            Console.WriteLine();

            var sw = Stopwatch.StartNew();

            List<PartitionSimilarity> results = ragEngine.FindMatchingPartitions(
                query,
                topK: _topK,
                minScore: _minScore);

            sw.Stop();

            PrintStrategyBanner(_strategyName, _rerankEnabled, _mmrLambda, _contextWindow);

            if (results.Count == 0)
            {
                PrintStatus("  No results above minimum score threshold.", ConsoleColor.Yellow);
            }
            else
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var p = results[i];
                    string scoreInfo;

                    if (p.RerankedScore >= 0)
                    {
                        scoreInfo = $"score={p.Similarity:F3} (raw={p.RawSimilarity:F3}, rerank={p.RerankedScore:F3})";
                    }
                    else
                    {
                        scoreInfo = $"score={p.Similarity:F3}";
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"  #{i + 1} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"[{scoreInfo}] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(p.SectionIdentifier);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($" (partition {p.PartitionIndex})");

                    // Show a preview of the payload
                    string preview = GetPayloadPreview(p.Payload, maxLength: 120);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"     {preview}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  [{results.Count} results in {sw.Elapsed.TotalMilliseconds:F0}ms | " +
                              $"strategy={_strategyName} | topK={_topK} | minScore={_minScore:F2} | " +
                              $"mmr={_mmrLambda:F1} | ctx={_contextWindow}" +
                              (_rerankEnabled ? $" | rerank alpha={_rerankAlpha:F1}" : "") + "]");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void RunComparison(RagEngine ragEngine, string query, LM embeddingModel)
        {
            Console.WriteLine();
            PrintSection($"Comparison: \"{query}\"");
            Console.WriteLine();

            // Save current state
            var savedStrategy = ragEngine.RetrievalStrategy;
            var savedReranker = ragEngine.Reranker;
            float savedMmr = ragEngine.MmrLambda;
            int savedCtx = ragEngine.ContextWindow;

            var strategies = new (string Name, IRetrievalStrategy Strategy)[]
            {
                ("Vector", new VectorRetrievalStrategy()),
                ("BM25", new Bm25RetrievalStrategy()),
                ("Hybrid", new HybridRetrievalStrategy()),
            };

            // Collect results for all strategies
            var allResults = new List<(string Name, List<PartitionSimilarity> Results, double Ms)>();

            foreach (var (name, strategy) in strategies)
            {
                ragEngine.RetrievalStrategy = strategy;
                ragEngine.Reranker = null;
                ragEngine.MmrLambda = 1.0f;
                ragEngine.ContextWindow = 0;

                var sw = Stopwatch.StartNew();
                var results = ragEngine.FindMatchingPartitions(query, topK: _topK, minScore: 0.01f);
                sw.Stop();

                allResults.Add((name, results, sw.Elapsed.TotalMilliseconds));
            }

            // Hybrid + Rerank
            {
                ragEngine.RetrievalStrategy = new HybridRetrievalStrategy();
                ragEngine.Reranker = new RagEngine.RagReranker(embeddingModel, _rerankAlpha);
                ragEngine.MmrLambda = 1.0f;
                ragEngine.ContextWindow = 0;

                var sw = Stopwatch.StartNew();
                var results = ragEngine.FindMatchingPartitions(query, topK: _topK, minScore: 0.01f);
                sw.Stop();

                allResults.Add(($"Hybrid+Rerank(a={_rerankAlpha:F1})", results, sw.Elapsed.TotalMilliseconds));
            }

            // Hybrid + Rerank + MMR
            {
                ragEngine.RetrievalStrategy = new HybridRetrievalStrategy();
                ragEngine.Reranker = new RagEngine.RagReranker(embeddingModel, _rerankAlpha);
                ragEngine.MmrLambda = 0.7f;
                ragEngine.ContextWindow = 0;

                var sw = Stopwatch.StartNew();
                var results = ragEngine.FindMatchingPartitions(query, topK: _topK, minScore: 0.01f);
                sw.Stop();

                allResults.Add(($"Hybrid+Rerank+MMR(0.7)", results, sw.Elapsed.TotalMilliseconds));
            }

            // Print comparison table
            foreach (var (name, results, ms) in allResults)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  === {name} === ({ms:F0}ms, {results.Count} results)");
                Console.ResetColor();

                int display = Math.Min(results.Count, _topK);
                for (int i = 0; i < display; i++)
                {
                    var p = results[i];
                    string scoreStr = p.RerankedScore >= 0
                        ? $"{p.Similarity:F3} (raw={p.RawSimilarity:F3})"
                        : $"{p.Similarity:F3}";

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"    #{i + 1} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"[{scoreStr}] ");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"{p.SectionIdentifier} (p{p.PartitionIndex})");
                    Console.ResetColor();
                }

                if (results.Count == 0)
                {
                    PrintStatus("    (no results)", ConsoleColor.DarkGray);
                }

                Console.WriteLine();
            }

            // Restore state
            ragEngine.RetrievalStrategy = savedStrategy;
            ragEngine.Reranker = savedReranker;
            ragEngine.MmrLambda = savedMmr;
            ragEngine.ContextWindow = savedCtx;
        }

        // ─── Strategy Configuration ────────────────────────────────────────────

        static void ApplyStrategy(RagEngine ragEngine)
        {
            ragEngine.MmrLambda = _mmrLambda;
            ragEngine.ContextWindow = _contextWindow;

            ragEngine.RetrievalStrategy = _strategyName switch
            {
                "bm25" => new Bm25RetrievalStrategy(),
                "hybrid" => new HybridRetrievalStrategy(),
                _ => new VectorRetrievalStrategy()
            };
        }

        // ─── Command Handling ──────────────────────────────────────────────────

        static void HandleCommand(string input, RagEngine ragEngine, LM embeddingModel)
        {
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/vector":
                    _strategyName = "vector";
                    PrintStatus("Strategy: Vector (cosine similarity)", ConsoleColor.Green);
                    break;

                case "/bm25":
                    _strategyName = "bm25";
                    PrintStatus("Strategy: BM25 (keyword-based lexical ranking)", ConsoleColor.Green);
                    break;

                case "/hybrid":
                    _strategyName = "hybrid";
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[1], out float vw) &&
                        float.TryParse(parts[2], out float kw))
                    {
                        PrintStatus($"Strategy: Hybrid (vector={vw:F1}, keyword={kw:F1})", ConsoleColor.Green);
                    }
                    else
                    {
                        PrintStatus("Strategy: Hybrid (vector=1.0, keyword=1.0)", ConsoleColor.Green);
                    }
                    break;

                case "/rerank":
                    if (parts.Length >= 2)
                    {
                        string arg = parts[1].ToLowerInvariant();
                        if (arg == "on")
                        {
                            _rerankEnabled = true;
                            ragEngine.Reranker = new RagEngine.RagReranker(embeddingModel, _rerankAlpha);
                            PrintStatus($"Reranking enabled (alpha={_rerankAlpha:F1})", ConsoleColor.Green);
                        }
                        else if (arg == "off")
                        {
                            _rerankEnabled = false;
                            ragEngine.Reranker = null;
                            PrintStatus("Reranking disabled", ConsoleColor.Yellow);
                        }
                        else if (float.TryParse(arg, out float alpha) && alpha >= 0 && alpha <= 1)
                        {
                            _rerankAlpha = alpha;
                            _rerankEnabled = true;
                            ragEngine.Reranker = new RagEngine.RagReranker(embeddingModel, _rerankAlpha);
                            PrintStatus($"Reranking enabled (alpha={_rerankAlpha:F1})", ConsoleColor.Green);
                        }
                    }
                    else
                    {
                        PrintStatus($"Reranking is {(_rerankEnabled ? "ON" : "OFF")} (alpha={_rerankAlpha:F1})", ConsoleColor.White);
                        PrintStatus("  Usage: /rerank on | /rerank off | /rerank 0.7", ConsoleColor.DarkGray);
                    }
                    break;

                case "/mmr":
                    if (parts.Length >= 2 && float.TryParse(parts[1], out float lambda) && lambda >= 0 && lambda <= 1)
                    {
                        _mmrLambda = lambda;
                        string desc = _mmrLambda >= 1.0f
                            ? "disabled (pure relevance)"
                            : _mmrLambda >= 0.7f
                                ? "mild diversity"
                                : "strong diversity";
                        PrintStatus($"MMR lambda={_mmrLambda:F1} ({desc})", ConsoleColor.Green);
                    }
                    else
                    {
                        PrintStatus($"Current MMR lambda: {_mmrLambda:F1}", ConsoleColor.White);
                        PrintStatus("  Usage: /mmr 0.7  (range 0.0=max diversity to 1.0=pure relevance)", ConsoleColor.DarkGray);
                    }
                    break;

                case "/context":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int ctx) && ctx >= 0)
                    {
                        _contextWindow = ctx;
                        PrintStatus($"Context window: {_contextWindow} neighboring partitions", ConsoleColor.Green);
                    }
                    else
                    {
                        PrintStatus($"Current context window: {_contextWindow}", ConsoleColor.White);
                        PrintStatus("  Usage: /context 2  (0=disabled, N=include N neighbors each side)", ConsoleColor.DarkGray);
                    }
                    break;

                case "/topk":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int k) && k > 0)
                    {
                        _topK = k;
                        PrintStatus($"Top-K set to {_topK}", ConsoleColor.Green);
                    }
                    else
                    {
                        PrintStatus($"Current top-K: {_topK}", ConsoleColor.White);
                        PrintStatus("  Usage: /topk 5", ConsoleColor.DarkGray);
                    }
                    break;

                case "/minscore":
                    if (parts.Length >= 2 && float.TryParse(parts[1], out float ms) && ms >= 0 && ms <= 1)
                    {
                        _minScore = ms;
                        PrintStatus($"Min score set to {_minScore:F2}", ConsoleColor.Green);
                    }
                    else
                    {
                        PrintStatus($"Current min score: {_minScore:F2}", ConsoleColor.White);
                        PrintStatus("  Usage: /minscore 0.3", ConsoleColor.DarkGray);
                    }
                    break;

                case "/compare":
                    if (parts.Length < 2)
                    {
                        PrintStatus("Usage: /compare <your query>", ConsoleColor.Yellow);
                        PrintStatus("  Runs the same query across all strategies for side-by-side comparison.", ConsoleColor.DarkGray);
                    }
                    else
                    {
                        string query = string.Join(' ', parts.Skip(1));
                        RunComparison(ragEngine, query, embeddingModel);
                    }
                    break;

                case "/stats":
                    PrintSection("Current Configuration");
                    PrintStatus($"  Strategy:         {_strategyName}", ConsoleColor.White);
                    PrintStatus($"  Top-K:            {_topK}", ConsoleColor.White);
                    PrintStatus($"  Min score:        {_minScore:F2}", ConsoleColor.White);
                    PrintStatus($"  MMR lambda:       {_mmrLambda:F1}" +
                                (_mmrLambda < 1.0f ? " (diversity enabled)" : " (disabled)"), ConsoleColor.White);
                    PrintStatus($"  Context window:   {_contextWindow}" +
                                (_contextWindow > 0 ? " neighbors" : " (disabled)"), ConsoleColor.White);
                    PrintStatus($"  Reranking:        {(_rerankEnabled ? $"ON (alpha={_rerankAlpha:F1})" : "OFF")}", ConsoleColor.White);
                    PrintStatus($"  Data sources:     {ragEngine.DataSources.Count}", ConsoleColor.White);

                    int totalPartitions = 0;
                    foreach (var ds in ragEngine.DataSources)
                    {
                        foreach (var sec in ds.Sections)
                        {
                            totalPartitions += sec.Partitions.Count;
                        }
                    }

                    PrintStatus($"  Total partitions: {totalPartitions}", ConsoleColor.White);
                    break;

                case "/help":
                    PrintCommands();
                    break;

                default:
                    PrintStatus($"Unknown command: {cmd}. Type /help for available commands.", ConsoleColor.Yellow);
                    break;
            }
        }

        // ─── Model Loading ─────────────────────────────────────────────────────

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        // ─── Sample Knowledge Base ─────────────────────────────────────────────
        //
        // Fictional "NebulaDB" database documentation. Designed so that:
        // - Some queries are keyword-heavy (error codes, config keys) where BM25 shines
        // - Some queries are semantic (conceptual questions) where vector search shines
        // - Some sections overlap in content, making MMR diversity useful
        // - Varied section lengths test chunk boundary handling

        static List<(string Topic, string Content)> GetSampleKnowledge()
        {
            return new List<(string, string)>
            {
                ("Getting Started", """
                    NebulaDB is a distributed document database designed for cloud-native applications.
                    It stores data as flexible JSON documents organized into collections within namespaces.
                    A namespace is a logical grouping of collections that share the same access policies
                    and resource quotas.

                    To install NebulaDB, use the package manager: `nebula install --version 4.2`.
                    The default installation creates a single-node cluster listening on port 9247.
                    For production deployments, a minimum of three nodes is recommended to ensure
                    high availability through automatic leader election and data replication.

                    The NebulaDB CLI (`nebula-cli`) provides interactive access to the database.
                    Connect with: `nebula-cli --host localhost --port 9247 --auth-token <token>`.
                    The CLI supports tab completion, query history, and output formatting in JSON,
                    table, or CSV modes.
                    """),

                ("Connection Management", """
                    NebulaDB client connections are managed through a connection pool. The default pool
                    size is 10 connections per node, configurable via `connection.pool.size` in the
                    client configuration file (`nebula-client.yaml`).

                    Connection strings follow the format:
                    `nebula://<user>:<password>@<host>:<port>/<namespace>?option=value`

                    Supported connection options:
                    - `timeout=30s`: connection timeout (default 30 seconds)
                    - `retries=3`: automatic retry count for transient failures
                    - `tls=true`: enable TLS encryption (required in production)
                    - `pool_size=10`: override default connection pool size
                    - `read_preference=secondary`: route reads to replica nodes

                    When a connection attempt fails, the client raises error NDB-1001 (connection refused)
                    or NDB-1002 (authentication failed). Transient failures such as NDB-1003 (connection
                    reset) are automatically retried up to the configured retry count.

                    For applications that need graceful degradation, implement the `IConnectionListener`
                    interface to receive notifications about connection state changes. The listener
                    provides callbacks for `OnConnected`, `OnDisconnected`, `OnReconnecting`, and
                    `OnConnectionPoolExhausted` events.
                    """),

                ("Query Language", """
                    NebulaDB uses NebulaQL, a SQL-like query language designed for document databases.
                    Basic syntax: `SELECT <fields> FROM <collection> WHERE <conditions>`.

                    Field access uses dot notation for nested documents: `SELECT user.address.city`.
                    Array elements are accessed with bracket notation: `SELECT tags[0]`.

                    Supported operators:
                    - Comparison: =, !=, <, >, <=, >=
                    - Logical: AND, OR, NOT
                    - Pattern matching: LIKE, REGEX
                    - Array: CONTAINS, ANY, ALL
                    - Null checking: IS NULL, IS NOT NULL
                    - Range: BETWEEN ... AND ...

                    Aggregation functions: COUNT, SUM, AVG, MIN, MAX, DISTINCT.
                    Grouping: `GROUP BY <field>` with optional `HAVING <condition>`.

                    Joins between collections use the LOOKUP keyword:
                    `SELECT * FROM orders LOOKUP customers ON orders.customer_id = customers._id`

                    Query execution plans can be inspected with `EXPLAIN <query>` which shows the
                    index usage, estimated row count, and execution cost.

                    Performance tip: always create indexes on fields used in WHERE clauses. Without
                    an index, NebulaDB performs a full collection scan which is acceptable for small
                    collections but degrades rapidly beyond 100,000 documents.
                    """),

                ("Indexing", """
                    NebulaDB supports several index types to optimize query performance.

                    Single-field index: `CREATE INDEX idx_name ON collection(field)`.
                    Compound index: `CREATE INDEX idx_name ON collection(field1, field2)`.
                    Text index: `CREATE TEXT INDEX idx_name ON collection(field)` for full-text search.
                    Geospatial index: `CREATE GEO INDEX idx_name ON collection(location)` for proximity queries.
                    Vector index: `CREATE VECTOR INDEX idx_name ON collection(embedding) DIMENSION 384`
                    for similarity search.

                    Index build status can be monitored with `SHOW INDEX STATUS`. Building large indexes
                    runs in the background and does not block read or write operations.

                    Index selection is automatic. The query optimizer evaluates available indexes and
                    chooses the most selective one. Force a specific index with the `USE INDEX(idx_name)` hint.

                    Index maintenance: indexes are updated incrementally on each write. For bulk imports,
                    disable auto-indexing with `SET auto_index = false`, load the data, then rebuild
                    with `REINDEX collection`. This approach is 5 to 10 times faster for large imports.

                    Unused indexes waste storage and slow down writes. Run `SHOW INDEX USAGE` periodically
                    to identify indexes with zero or near-zero query hits.
                    """),

                ("Replication and High Availability", """
                    NebulaDB uses a Raft-based consensus protocol for data replication. Each collection
                    is divided into shards, and each shard maintains a configurable number of replicas
                    (default: 3). One replica is the leader that handles all writes; the others are
                    followers that replicate the leader's write-ahead log.

                    Replication factor is set per collection:
                    `CREATE COLLECTION orders WITH replication_factor = 3`

                    When a leader node fails, the Raft protocol triggers an automatic election. A new
                    leader is typically elected within 2 to 5 seconds. During election, write operations
                    return error NDB-3001 (leader unavailable) and should be retried by the client.

                    Read consistency levels:
                    - `eventual`: reads from any replica (fastest, may return stale data)
                    - `session`: reads reflect all writes from the same session
                    - `strong`: reads reflect all committed writes (slowest, most consistent)

                    Set consistency per query: `SELECT * FROM orders WITH consistency = 'strong'`

                    For disaster recovery, enable cross-region replication with:
                    `ALTER COLLECTION orders SET cross_region = 'us-east-1,eu-west-1'`
                    Cross-region replicas are asynchronous and have a typical lag of 50 to 200ms.

                    Monitor replication health with `SHOW REPLICATION STATUS` which displays lag,
                    leader identity, and follower sync positions for each shard.
                    """),

                ("Replication Troubleshooting", """
                    Common replication issues and their resolution:

                    NDB-3001 (Leader Unavailable): occurs during leader election or network partition.
                    The client should retry with exponential backoff. If the error persists for more
                    than 30 seconds, check cluster health with `nebula-cli cluster status`.

                    NDB-3002 (Replication Lag Exceeded): a follower has fallen too far behind the leader.
                    Check the follower's disk I/O and network bandwidth. If the follower cannot catch up,
                    it will enter recovery mode and rebuild its state from a snapshot.

                    NDB-3003 (Split Brain Detected): two nodes believe they are the leader for the same
                    shard. This is a critical error that requires immediate attention. Stop the cluster,
                    run `nebula-admin resolve-split-brain --shard <id>`, and restart nodes one at a time.

                    NDB-3004 (Snapshot Transfer Failed): occurs when a new replica joins and cannot
                    download the initial snapshot. Verify network connectivity and ensure the source
                    node has sufficient disk space for snapshot creation.

                    Preventive measures: ensure all nodes have synchronized clocks (NTP drift < 50ms),
                    use dedicated network interfaces for replication traffic, and monitor disk usage
                    to avoid write-ahead log accumulation when followers are offline.
                    """),

                ("Error Reference", """
                    NebulaDB error codes follow the pattern NDB-XXXX where the first digit indicates
                    the error category.

                    1xxx: Connection Errors
                    - NDB-1001: Connection refused. The target node is not running or the port is blocked.
                    - NDB-1002: Authentication failed. Check credentials and token expiration.
                    - NDB-1003: Connection reset. Transient network issue; retry automatically.
                    - NDB-1004: TLS handshake failed. Certificate mismatch or expiration.
                    - NDB-1005: Connection pool exhausted. Increase pool_size or reduce concurrent queries.

                    2xxx: Query Errors
                    - NDB-2001: Syntax error in NebulaQL query. Check query structure.
                    - NDB-2002: Unknown collection. Verify the collection name and namespace.
                    - NDB-2003: Field not found. The specified field does not exist in the schema.
                    - NDB-2004: Type mismatch. The value type does not match the field type.
                    - NDB-2005: Query timeout. The query exceeded the configured timeout limit.

                    3xxx: Replication Errors
                    - NDB-3001: Leader unavailable. Retry with backoff during leader election.
                    - NDB-3002: Replication lag exceeded threshold.
                    - NDB-3003: Split brain detected. Requires manual intervention.
                    - NDB-3004: Snapshot transfer failed.

                    4xxx: Resource Errors
                    - NDB-4001: Out of memory. Reduce query complexity or add memory.
                    - NDB-4002: Disk space exhausted. Free space or expand storage.
                    - NDB-4003: Rate limit exceeded. Reduce request frequency.
                    - NDB-4010: Shard migration in progress. Retry after migration completes.
                    - NDB-4011: Node overloaded. The node's CPU usage exceeds 90%.
                    - NDB-4012: Request timeout. The operation exceeded the server-side timeout of 60s.
                    """),

                ("Security and Authentication", """
                    NebulaDB supports multiple authentication mechanisms:

                    Token-based authentication (default): generate tokens with
                    `nebula-admin create-token --user admin --ttl 24h`. Tokens are JWT-based
                    and include the user's roles and namespace permissions.

                    LDAP integration: configure in `nebula-server.yaml`:
                    ```
                    auth:
                      provider: ldap
                      ldap_url: ldap://directory.example.com:389
                      base_dn: dc=example,dc=com
                      user_filter: (uid={username})
                    ```

                    Role-based access control (RBAC) provides granular permissions:
                    - `admin`: full cluster management
                    - `db_owner`: create/drop collections within a namespace
                    - `read_write`: read and write documents
                    - `read_only`: read documents only
                    - `backup_operator`: create and restore backups

                    Custom roles can be created: `CREATE ROLE analyst WITH permissions = ['read', 'aggregate']`

                    Data encryption: NebulaDB encrypts data at rest using AES-256-GCM. The encryption
                    key is stored in an external key management service (KMS). Supported KMS providers:
                    AWS KMS, Azure Key Vault, HashiCorp Vault.

                    Audit logging records all authentication attempts, permission changes, and data
                    access events. Enable with `SET audit_log = true`. Logs are written to
                    `/var/log/nebuladb/audit.log` in JSON format.
                    """),

                ("Backup and Recovery", """
                    NebulaDB provides several backup strategies for data protection.

                    Full backup: `nebula-admin backup --type full --destination s3://bucket/path`
                    Creates a consistent snapshot of all collections. During backup, write operations
                    continue normally using a copy-on-write mechanism.

                    Incremental backup: `nebula-admin backup --type incremental --base <backup-id>`
                    Captures only changes since the specified base backup. Incremental backups are
                    typically 10 to 50 times smaller than full backups.

                    Point-in-time recovery (PITR): enable with `SET pitr_retention = 7d`.
                    This retains the write-ahead log for the specified duration, allowing recovery
                    to any point in time: `nebula-admin restore --pitr "2026-01-15T14:30:00Z"`

                    Backup destinations: local filesystem, Amazon S3, Azure Blob Storage,
                    Google Cloud Storage, or any S3-compatible endpoint.

                    Restore procedure:
                    1. Stop the cluster: `nebula-admin cluster stop`
                    2. Restore: `nebula-admin restore --backup <backup-id> --target <data-dir>`
                    3. Start the cluster: `nebula-admin cluster start`
                    4. Verify: `nebula-admin verify --collection <name>`

                    Automated backup schedule: configure in `nebula-server.yaml`:
                    ```
                    backup:
                      schedule: "0 2 * * *"  # Daily at 2 AM
                      type: incremental
                      full_every: 7           # Full backup every 7 days
                      destination: s3://backup-bucket/nebuladb/
                      retention: 30d
                    ```

                    Best practice: test restores regularly. An untested backup is not a backup.
                    """),

                ("Performance Tuning", """
                    Key performance configuration parameters for NebulaDB:

                    Memory allocation: `memory.cache_size = 4GB` controls the document cache.
                    Larger cache reduces disk I/O for frequently accessed documents. Monitor cache
                    hit rate with `SHOW METRICS WHERE name = 'cache_hit_ratio'`. A ratio below 0.8
                    indicates the cache is too small.

                    Write performance: `write.batch_size = 1000` groups writes into batches for
                    efficiency. `write.sync_mode = 'async'` defers fsync for higher throughput at
                    the risk of losing the last few milliseconds of writes on crash.

                    Read performance: `read.prefetch_size = 100` pre-loads documents for sequential
                    scans. For point lookups, set `read.bloom_filter = true` to avoid unnecessary
                    disk reads (reduces I/O by up to 90% for missing key lookups).

                    Query optimizer: `optimizer.cost_model = 'adaptive'` learns from query execution
                    history to improve index selection over time. Enable statistics collection with
                    `ANALYZE collection` after significant data changes.

                    Connection tuning: `connection.keep_alive = 60s` prevents idle connection cleanup.
                    `connection.max_concurrent_queries = 100` limits parallelism per node.

                    Compaction: NebulaDB uses tiered compaction by default. For write-heavy workloads,
                    switch to leveled compaction: `ALTER COLLECTION SET compaction = 'leveled'`.
                    Monitor compaction pressure with `SHOW METRICS WHERE name = 'compaction_pending'`.

                    Hardware recommendations: NVMe SSDs for storage, 10GbE network for replication,
                    minimum 16 GB RAM per node, CPU with AVX2 support for vectorized operations.
                    """),

                ("Monitoring and Observability", """
                    NebulaDB exposes metrics via a Prometheus-compatible endpoint at `/metrics` on
                    port 9248 (configurable). Key metrics to monitor:

                    Throughput: `nebuladb_queries_per_second`, `nebuladb_writes_per_second`
                    Latency: `nebuladb_query_duration_p50`, `nebuladb_query_duration_p99`
                    Resources: `nebuladb_memory_used`, `nebuladb_disk_used`, `nebuladb_cpu_usage`
                    Replication: `nebuladb_replication_lag_ms`, `nebuladb_leader_elections_total`
                    Errors: `nebuladb_errors_total{code="NDB-XXXX"}`

                    Distributed tracing: enable with `SET tracing = true`. Each query receives a
                    trace ID that can be correlated across nodes. Export traces to Jaeger or Zipkin
                    using the OpenTelemetry exporter.

                    Slow query log: queries exceeding the configured threshold are logged to
                    `/var/log/nebuladb/slow-queries.log`. Configure with:
                    `SET slow_query_threshold = 500ms`

                    Health checks: `GET /health` returns node status, `GET /ready` indicates
                    readiness to accept traffic. Use these endpoints for load balancer configuration.

                    Alerting recommendations:
                    - Alert on replication lag > 1 second
                    - Alert on disk usage > 80%
                    - Alert on error rate > 1% of total queries
                    - Alert on leader election frequency > 1 per hour
                    - Alert on cache hit ratio < 0.7
                    """)
            };
        }

        // ─── Console Helpers ───────────────────────────────────────────────────

        static string GetPayloadPreview(string payload, int maxLength)
        {
            string clean = payload
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Replace('\t', ' ');

            // Collapse multiple spaces
            while (clean.Contains("  "))
            {
                clean = clean.Replace("  ", " ");
            }

            clean = clean.Trim();

            if (clean.Length > maxLength)
            {
                clean = clean.Substring(0, maxLength - 3) + "...";
            }

            return clean;
        }

        static void PrintStrategyBanner(string strategy, bool rerank, float mmr, int ctx)
        {
            var parts = new List<string> { strategy.ToUpper() };

            if (rerank)
            {
                parts.Add("Rerank");
            }

            if (mmr < 1.0f)
            {
                parts.Add($"MMR({mmr:F1})");
            }

            if (ctx > 0)
            {
                parts.Add($"Ctx({ctx})");
            }

            string label = string.Join(" + ", parts);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  === {label} ===");
            Console.ResetColor();
        }

        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  +============================================================+");
            Console.WriteLine("  |           Retrieval Quality Tuning Demo                    |");
            Console.WriteLine("  +============================================================+");
            Console.WriteLine("  |  Compare Vector, BM25, and Hybrid retrieval strategies.    |");
            Console.WriteLine("  |  Tune reranking, MMR diversity, and context windows.       |");
            Console.WriteLine("  |  See how different configurations affect result quality.    |");
            Console.WriteLine("  +============================================================+");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintSection(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  -- {title} --");
            Console.ResetColor();
        }

        static void PrintStatus(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"  {message}");
            Console.ResetColor();
        }

        static void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ------------------------------------------------------------");
            Console.ResetColor();
        }

        static void PrintCommands()
        {
            Console.WriteLine();
            PrintSection("Retrieval Strategies");
            Console.WriteLine("  /vector               Switch to vector search (cosine similarity)");
            Console.WriteLine("  /bm25                 Switch to BM25 (keyword-based lexical search)");
            Console.WriteLine("  /hybrid [vw kw]       Switch to hybrid (vector + BM25 with RRF fusion)");
            Console.WriteLine();
            PrintSection("Quality Tuning");
            Console.WriteLine("  /rerank on|off|<a>    Toggle reranking or set alpha (0=reranker only, 1=original only)");
            Console.WriteLine("  /mmr <lambda>         Set MMR diversity (0.0=max diversity, 1.0=pure relevance)");
            Console.WriteLine("  /context <n>          Include N neighboring partitions around each match");
            Console.WriteLine();
            PrintSection("Parameters");
            Console.WriteLine("  /topk <n>             Max results to retrieve");
            Console.WriteLine("  /minscore <s>         Minimum relevance score threshold (0.0 to 1.0)");
            Console.WriteLine();
            PrintSection("Analysis");
            Console.WriteLine("  /compare <query>      Run query across all strategies for side-by-side comparison");
            Console.WriteLine("  /stats                Show current configuration and index statistics");
            Console.WriteLine("  /help                 Show this help");
            Console.WriteLine("  (empty)               Exit");
        }
    }
}
