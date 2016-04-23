﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Crawly
{
    public class CrawlerEngine
    {
        private readonly HashSet<string> _crawledUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Regex _linkRegex = new Regex("((?:href=\"(?<protocol>/|http://|https://))(?<link>.+?)[\"])",
            RegexOptions.Compiled & RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(10));

        private readonly int MaxConcurrency = 10;

        public async Task<int> Crawl(string startUrl)
        {
            List<string> newLinks;
            var Urls = new Stack<string>();
            var tasks = new List<Task<List<string>>>();
            Urls.Push(startUrl);

            while (Urls.Count > 0)
            {
                while (tasks.Count < MaxConcurrency && Urls.Count > 0)
                {
                    tasks.Add(CrawlAndProcess(Urls.Pop()));
                }
                var completed = await Task.WhenAny(tasks.ToArray());
                tasks.Remove(completed);
                foreach (var link in completed.Result)
                {
                    Urls.Push(link);
                }
            }
           
            return _crawledUrls.Count;
        }

        public async Task<List<string>> CrawlAndProcess(string url)
        {
            var newLinks = await GetLinks(url);
            var returnLinks = new List<string>();
            foreach (var newLink in newLinks)
            {
                if (!_crawledUrls.Contains(newLink))
                {
                    PrintFoundMessage(newLink);
                    _crawledUrls.Add(newLink);
                    returnLinks.Add(newLink);
                }
            }
            return returnLinks;
        }

        private void PrintCrawlMessage(string url)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Crawling new {url}");
            Console.ResetColor();
        }

        private void PrintFoundMessage(string url)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Found new {url}");
            Console.ResetColor();
        }

        private void PrintErrorMessage(Exception exception, string url)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error on url {url} \n {exception}");
            Console.ResetColor();
        }


        public async Task<List<string>> GetLinks(string url)
        {
            PrintCrawlMessage(url);
            var parsedUrl = new Uri(url);
            var client = new HttpClient();
            var returnLinks = new List<string>();
            string response;

            try
            {
                response = await client.GetStringAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex, url);
                return returnLinks;
            }

            var links = _linkRegex.Matches(response);
            foreach (Match link in links)
            {
                var currLink = link.Groups["link"].Value;
                var currProtocol = link.Groups["protocol"].Value;
                if (currProtocol == "/")
                {
                    returnLinks.Add(parsedUrl.Scheme + "://" + parsedUrl.Host + currProtocol + currLink);
                }
                else
                {
                    returnLinks.Add(currProtocol + currLink);
                }
            }
            return returnLinks;
        }
    }
}