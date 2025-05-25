using LMKit.Data;
using LMKit.Embeddings;
using LMKit.Model;
using LMKit.Retrieval;

namespace image_similarity_search
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 1. Load the pre-trained image embedding model
            //    This model converts images into fixed-length float vectors that capture visual features.
            var model = LM.LoadFromModelID("nomic-embed-vision");

            // 2. Create or reset a local vector database on disk
            //    We’ll store image embeddings here for later similarity searches.
            //    If the file already exists, overwrite it to start fresh.
            const string CollectionPath = @"collection.ds";
            var collection = DataSource.CreateFileDataSource(
                path: CollectionPath,
                identifier: "my-image-collection",
                model: model,
                overwrite: true
            );

            // 3. Instantiate an embedder to compute image vectors
            //    The Embedder wraps the model and handles preprocessing (e.g. resizing, normalization).
            var embedder = new Embedder(model);

            // 4. Compute and store embeddings for several images
            //    Each call:
            //      a) Loads the image from disk.
            //      b) Runs it through the embedding model.
            //      c) Inserts the resulting float[] into our collection under a unique key.
            collection.Upsert(
                sectionIdentifier: "house1",
                vector: embedder.GetEmbeddings(new Attachment(@"house1.jpg"))
            );

            collection.Upsert(
                sectionIdentifier: "house2",
                vector: embedder.GetEmbeddings(new Attachment(@"house2.jpg"))
            );

            collection.Upsert(
                sectionIdentifier: "cat1",
                vector: embedder.GetEmbeddings(new Attachment(@"cat1.jpg"))
            );

            collection.Upsert(
                sectionIdentifier: "cat2",
                vector: embedder.GetEmbeddings(new Attachment(@"cat2.jpg"))
            );

            collection.Upsert(
                sectionIdentifier: "dog1",
                vector: embedder.GetEmbeddings(new Attachment(@"dog1.jpg"))
            );

            collection.Upsert(
                sectionIdentifier: "dog2",
                vector: embedder.GetEmbeddings(new Attachment(@"dog2.jpg"))
            );

            // 5. Compute the embedding for a new query image
            //    We’ll search for the most visually similar stored images.
            string queryImagePath = @"cat3.webp"; // Path to the query image.
            var queryVector = embedder.GetEmbeddings(new Attachment(queryImagePath));

            // 6. Perform the similarity search across our collection
            //    VectorSearch.FindMatchingPartitions returns TextPartitions ordered by cosine similarity.
            //    We expect "cat1" and "cat2" to be the closest matches.
            var similarPartitions = VectorSearch.FindMatchingPartitions(
                dataSources: [collection],
                model: model,
                vector: queryVector
            );

            // 7. Use the results
            //    Each returned partition contains its section identifier (e.g. "cat1")
            //    and similarity score—ideal for presenting the top-k closest images.

            Console.WriteLine("Top similar images:");
            Console.WriteLine("===================================");
            foreach (var partition in similarPartitions)
            {
                Console.WriteLine($"{partition.SectionIdentifier}: score = {partition.Similarity:F4}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}