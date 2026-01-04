# Image Similarity Search

A demo for finding visually similar images using vector embeddings with LM-Kit.NET. Build a local image database and query it by visual similarity.

## Features

- Convert images to vector embeddings using vision models
- Store embeddings in a local vector database
- Search for visually similar images by cosine similarity
- Support for common image formats: JPG, PNG, WebP, and more

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK

## How It Works

1. Load a pre-trained image embedding model (Nomic Embed Vision)
2. Create a local vector database to store image embeddings
3. Index your image collection by computing and storing embeddings
4. Query with a new image to find the most visually similar matches

## Usage

1. Place your reference images in the application directory
2. Run the application
3. The demo indexes sample images (houses, cats, dogs)
4. A query image is compared against the collection
5. Results are ranked by similarity score

## Sample Output

```
Top similar images:
===================================
cat1: score = 0.9234
cat2: score = 0.8876
dog1: score = 0.4521
...
```