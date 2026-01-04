using LMKit.Agents.Tools;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace function_calling
{
    internal class BookPlugin
    {
        private readonly HttpClient client = new();

        [LMFunction("GetAvailableBookCountByAuthor", "Retrieves the count of books available by a specified author.")]
        public async Task<int> GetAvailableBookCountByAuthor([Description("The name of the author whose available books are being counted.")] string author)
        {
            string url = "https://openlibrary.org/search.json?author=" + Uri.EscapeDataString(author); //api ref: https://openlibrary.org/dev/docs/api/search

            HttpResponseMessage response = await client.GetAsync(url);

            _ = response.EnsureSuccessStatusCode();

            Stream content = await response.Content.ReadAsStreamAsync();

            JsonDocument jsonDoc = JsonDocument.Parse(content);

            JsonElement numElement = jsonDoc.RootElement.GetProperty("num_found");

            if (numElement.TryGetInt32(out int count))
            {
                return count;
            }

            return 0;
        }

        [LMFunction("GetBookInfo", "Retrieves detailed information about a specified book.")]
        public async Task<string> GetBookInfo([Description("The title of the book to retrieve information for.")] string title)
        {
            string url = "https://openlibrary.org/search.json?title=" + Uri.EscapeDataString(title); //api ref: https://openlibrary.org/dev/docs/api/search

            HttpResponseMessage response = await client.GetAsync(url);

            _ = response.EnsureSuccessStatusCode();

            Stream content = await response.Content.ReadAsStreamAsync();

            JsonDocument jsonDoc = JsonDocument.Parse(content);
            var docs = jsonDoc.RootElement.GetProperty("docs");
            List<JsonElement> docsArray = docs.EnumerateArray()
                                              .Cast<JsonElement>()
                                              .ToList();

            string firstPublishYear = "unknown";
            string authors = "unknown";
            string numberOfPagesMedian = "unknown";
            string isbn = "unknown";
            string publisher = "unknown";
            string formats = "unknown";
            string ebookAccess = "unknown";

            if (docsArray.Count > 0)
            {
                JsonElement publishYearElement = docsArray[0].GetProperty("first_publish_year");
                firstPublishYear = publishYearElement.GetInt32().ToString();
                if (docsArray[0].TryGetProperty("number_of_pages_median", out _))
                {
                    numberOfPagesMedian = publishYearElement[0].GetInt32().ToString();
                }

                if (docsArray[0].TryGetProperty("ebook_access", out JsonElement ebookAccessElement))
                {
                    ebookAccess = ebookAccessElement.GetString();
                }

                if (docsArray[0].TryGetProperty("isbn", out JsonElement isbnElement))
                {
                    isbn = NormalizeSpacings(isbnElement.GetRawText().Replace("\n", " "));
                }

                if (docsArray[0].TryGetProperty("format", out JsonElement formatElement))
                {
                    formats = NormalizeSpacings(formatElement.GetRawText().Replace("\n", " "));
                }

                if (docsArray[0].TryGetProperty("publisher", out JsonElement publisherProperty))
                {
                    publisher = publisherProperty[0].GetString();
                }

                if (docsArray[0].TryGetProperty("author_name", out JsonElement authorsElement))
                {
                    authors = NormalizeSpacings(authorsElement.GetRawText().Replace("\n", " "));
                }

                if (docsArray[0].TryGetProperty("title", out JsonElement titleElement))
                {
                    title = titleElement.GetString();
                }
            }

            return "Title: " + title + "\n" +
                   "First publish year: " + firstPublishYear + "\n" +
                   "Authors: " + authors + "\n" +
                   "Number of pages median: " + numberOfPagesMedian + "\n" +
                   "Formats: " + formats + "\n" +
                   "E-book access: " + ebookAccess + "\n" +
                   "ISBN: " + isbn + "\n" +
                   "Publisher: " + publisher;
        }

        [LMFunction("GetLastBookFromAuthor", "Retrieves detailed information about the most recent book by a specified author.")]
        public async Task<string> GetLastBookFromAuthor([Description("The name of the author whose latest book's information is being retrieved.")] string author)
        {
            string url = "https://openlibrary.org/search.json?author=" + Uri.EscapeDataString(author) + "&sort=new"; //api ref: https://openlibrary.org/dev/docs/api/search

            HttpResponseMessage response = await client.GetAsync(url);

            _ = response.EnsureSuccessStatusCode();

            Stream content = await response.Content.ReadAsStreamAsync();

            JsonDocument jsonDoc = JsonDocument.Parse(content);

            var docs = jsonDoc.RootElement.GetProperty("docs");
            List<JsonElement> docsArray = docs.EnumerateArray()
                                                .Cast<JsonElement>()
                                                .ToList();

            string title = "unknown";
            string publish_year = "unknown";

            if (docsArray.Count > 0)
            {
                JsonElement titleElement = docsArray[0].GetProperty("title");

                title = titleElement.GetString();

                List<JsonElement> publishYear = docsArray[0].GetProperty("publish_year").EnumerateArray()
                                                                                 .Cast<JsonElement>()
                                                                                 .ToList();
                if (publishYear.Count > 0)
                {
                    publish_year = publishYear[0].GetInt32().ToString();
                }
            }

            return title + ", publish year: " + publish_year;
        }


        [LMFunction("GetBookAuthor", "Retrieves the author's name for a specified book.")]
        public async Task<string> GetBookAuthor([Description("The title of the book to retrieve the author's name for.")] string book)
        {
            string url = "https://openlibrary.org/search.json?q=" + Uri.EscapeDataString(book); //api ref: https://openlibrary.org/dev/docs/api/search


            HttpResponseMessage response = await client.GetAsync(url);

            _ = response.EnsureSuccessStatusCode();

            Stream content = await response.Content.ReadAsStreamAsync();

            JsonDocument jsonDoc = JsonDocument.Parse(content);

            var docs = jsonDoc.RootElement.GetProperty("docs");
            List<JsonElement> docsArray = docs.EnumerateArray()
                                                .Cast<JsonElement>()
                                                .ToList();

            if (docsArray.Count > 0)
            {
                JsonElement authorElement = docsArray[0].GetProperty("author_name");

                List<JsonElement> authorArray = authorElement.EnumerateArray()
                                                             .Cast<JsonElement>()
                                                             .ToList();
                if (authorArray.Count > 0)
                {
                    string author = authorArray[0].GetString();

                    if (author != null)
                    {
                        return author;
                    }
                }
            }

            return "unknown";
        }

        private static string NormalizeSpacings(string text)
        {
            text = new Regex("[ ]{2,}", RegexOptions.None).Replace(text.Replace("\t", ""), " ").Replace("\r\n", "\n").Replace("\n ", "\n").Trim();
            text = Regex.Replace(text, "(\\n){2,}", "\n", RegexOptions.IgnoreCase);

            return text;
        }
    }
}