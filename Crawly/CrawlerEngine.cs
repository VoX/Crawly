using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

        private readonly ConcurrentDictionary<string, string[]> _robotRestricted =
            new ConcurrentDictionary<string, string[]>();

        private int? _maxConcurrency;

        private int MaxConcurrency
        {
            get
            {
                if (!_maxConcurrency.HasValue)
                {
                    int concurrency;
                    _maxConcurrency = !int.TryParse(ConfigurationManager.AppSettings["MaxConcurrency"],
                        out concurrency)
                        ? 1000
                        : concurrency;
                }

                return _maxConcurrency.Value;
            }
        }

        public async Task<int> Crawl(string startUrl)
        {
            var urls = new Stack<Uri>();
            var tasks = new List<Task<List<Uri>>>();
            urls.Push(new Uri(startUrl));

            while (urls.Count > 0 || tasks.Count > 0)
            {
                while (tasks.Count < MaxConcurrency && urls.Count > 0)
                {
                    tasks.Add(CrawlAndProcess(urls.Pop()));
                }
                var completed = await Task.WhenAny(tasks.ToArray());
                tasks.Remove(completed);
                foreach (var link in completed.Result)
                {
                    urls.Push(link);
                }
            }

            return _crawledUrls.Count;
        }

        public async Task<string[]> RobotRestrictions(Uri url)
        {
            var client = new HttpClient();
            var response =
                await
                    client.GetStringAsync(url.Scheme + Uri.SchemeDelimiter + url.Host + "/robots.txt")
                        .ConfigureAwait(false);
            return (from line in response.Split('\n')
                where line.StartsWith("Disallow:")
                let disallowed = line.Substring("Disallow:".Length).Trim()
                where !string.IsNullOrWhiteSpace(disallowed)
                select PrintDisallowedMessage(disallowed, url)).ToArray();
        }

        public async Task<bool> IsRobotRestricted(Uri url)
        {
            if (!_robotRestricted.ContainsKey(url.Host))
            {
                try
                {
                    var restrictions = await RobotRestrictions(url);
                    _robotRestricted.AddOrUpdate(url.Host, s => restrictions, (s, strings) => restrictions);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return _robotRestricted[url.Host].Any(prefix => url.PathAndQuery.StartsWith(prefix));
        }

        public async Task<List<Uri>> CrawlAndProcess(Uri url)
        {
            var newLinks = await GetLinks(url);
            var returnLinks = new List<Uri>();
            foreach (var newLink in newLinks)
            {
                if (!_crawledUrls.Contains(newLink.OriginalString))
                {
                    var restricted = await IsRobotRestricted(newLink);
                    if (!restricted)
                    {
                        PrintFoundMessage(newLink.OriginalString);
                        _crawledUrls.Add(newLink.OriginalString);
                        returnLinks.Add(newLink);
                    }
                }
            }
            return returnLinks;
        }

        private static void PrintCrawlMessage(string url)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Crawling new {url}");
            Console.ResetColor();
        }

        private static void PrintFoundMessage(string url)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Found new {url}");
            Console.ResetColor();
        }

        private static string PrintDisallowedMessage(string disallowed, Uri url)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"{url.Host} Disallowed {disallowed}");
            Console.ResetColor();
            return disallowed;
        }

        private static void PrintErrorMessage(Exception exception, string url)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error on url {url} \n {exception}");
            Console.ResetColor();
        }


        public async Task<List<Uri>> GetLinks(Uri url)
        {
            PrintCrawlMessage(url.OriginalString);
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            var returnLinks = new List<Uri>();
            string response;

            try
            {
                response = await client.GetStringAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex, url.OriginalString);
                return returnLinks;
            }

            var links = _linkRegex.Matches(response);
            foreach (Match link in links)
            {
                var currLink = link.Groups["link"].Value;
                var currProtocol = link.Groups["protocol"].Value;
                if (currProtocol == "/")
                {
                    returnLinks.Add(new Uri(url.Scheme + Uri.SchemeDelimiter + url.Host + currProtocol + currLink));
                }
                else
                {
                    returnLinks.Add(new Uri(currProtocol + currLink));
                }
            }
            return returnLinks;
        }
    }
}