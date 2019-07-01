using System;
using Newtonsoft.Json;
using HtmlAgilityPack;
using RestSharp;

using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;

namespace website_scraper
{
    class Program
    {
        static RestClient client = new RestClient("http://fitgirl-repacks.site/");
        static ConcurrentBag<postData> articles = new ConcurrentBag<postData>();
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                findPost(args[0]);
                return;
            }

            Console.WriteLine("Getting maximum page count...\n");
            var res = client.Get(new RestRequest(""));

            if (!res.IsSuccessful)
            {
                Console.WriteLine(res.ErrorMessage);
                throw res.ErrorException;
            }

            var html = new HtmlDocument();
            html.LoadHtml(res.Content);

            var pageNumbers = html.DocumentNode.SelectNodes("//body/div/div/div/div/div/nav/div/a[(@class='page-numbers') and not(@class='next')]");

            int maxPageCount = pageNumbers.Max(i => int.Parse(i.InnerText));
            
            int progress = 0;

            //for (int page = 65; page <= 75; page++)
            Parallel.For(0, maxPageCount, page =>
            {
                var response = client.Get(new RestRequest($"/page/{page + 1}/"));

                var htmldoc = new HtmlDocument();
                htmldoc.LoadHtml(System.Net.WebUtility.HtmlDecode(response.Content));

                var posts = htmldoc.DocumentNode.SelectNodes("//body/div/div/div/div/div/article");

                foreach (var item in posts)
                {
                    if (item.SelectSingleNode("header/div/span/a").InnerText == "Uncategorized")
                        continue;

                    var childNodes = item.SelectSingleNode("div/p").ChildNodes;
                    string originalSize = "0 MB";
                    string repackSize = "0 MB";

                    for (int i = 0; i < childNodes.Count; i++)
                    {
                        var data = childNodes[i];
                        if (data.Name == "#text" && Regex.IsMatch(data.InnerText, "Original Size"))
                        {
                            originalSize = childNodes[i+1].InnerText;
                            break;
                        }
                    }

                    for (int i = 0; i < childNodes.Count; i++)
                    {
                        var data = childNodes[i];
                        if (data.Name == "#text" && Regex.IsMatch(data.InnerText, "Repack Size"))
                        {
                            repackSize = childNodes[i+1].InnerText;
                            break;
                        }
                    }

                    articles.Add(new postData()
                    {
                        postID = item.Id,
                        timestamp = DateTime.Parse(item.SelectSingleNode("header/div[@class='entry-meta']/span[@class='entry-date']/a/time").GetAttributeValue("datetime", null)),
                        postTitle = item.SelectSingleNode("header/h1/a").InnerText,
                        postLink = item.SelectSingleNode("header/h1/a").GetAttributeValue("href", ""),
                        postFileOriginalSize = originalSize,
                        postFileRepackSize = repackSize
                    });
                }

                Interlocked.Increment(ref progress);
                //Console.Write($"\rCurrent page : {progress}");
                Console.WriteLine($"Current page : {progress}");
            }
            );

            var sortedArticles = articles.ToList().OrderByDescending(i => i.timestamp);
            System.IO.File.WriteAllText("postsdump.json", JsonConvert.SerializeObject(sortedArticles, Formatting.Indented));
        }

        // ! TODO: Fix this function.
        static void findPost(string searchQuery)
        {
            postData[] posts = JsonConvert.DeserializeObject<postData[]>(System.IO.File.ReadAllText("postsdump.json"));
            
            postData bestMatch = new postData();
            int bestScore = 100;

            foreach (var i in posts)
            {
                int distance = LevenshteinDistance.Compute(i.postTitle, searchQuery);
                if (distance < bestScore)
                    {
                        bestMatch = i;
                        bestScore = distance;
                    }
            }

            Console.WriteLine(bestMatch.postLink);

            return;

            Console.WriteLine(Array.Find(posts, i => 
            {
                return (LevenshteinDistance.Compute(i.postTitle, searchQuery) > 0);
            }).postLink);
        }
    }

    public class postData
    {
        public string postID {get; set;}
        public DateTime timestamp {get; set;}
        public string postTitle {get; set;}
        public string postLink {get; set;}
        public string postFileOriginalSize {get; set;}
        public string postFileRepackSize {get; set;}
    }

    static class LevenshteinDistance
    {
        public static int Compute(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++);
            for (int j = 1; j <= m; d[0, j] = j++);

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }
    }
}