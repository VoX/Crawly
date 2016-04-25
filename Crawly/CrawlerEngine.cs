using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Crawly
{
    public class CrawlerEngine
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly HashSet<string> _crawledUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Regex _linkRegex = new Regex("((?:href=\"(?<protocol>/|http://|https://))(?<link>.+?)[\"])",
            RegexOptions.Compiled & RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

        private readonly ResultsLogger _logger;

        private readonly ConcurrentDictionary<string, string[]> _robotRestricted =
            new ConcurrentDictionary<string, string[]>();


        private long _errorCounter;
        private long _crawledCounter;
        private int? _maxConcurrency;
        private List<Task<List<Uri>>> _tasks;
        private Stack<Uri> _urls;

        public CrawlerEngine(ResultsLogger logger = null, int maxConcurrency = 200)
        {
            MaxConcurrency = maxConcurrency;
            _client.Timeout = TimeSpan.FromSeconds(1);
            _logger = logger ?? new ResultsLogger(".\\crawl-" + Guid.NewGuid() + ".log");
            MaxConcurrency = maxConcurrency;
        }

        public string StartUrl { get; set; }
        public long Crawled => _crawledCounter;
        public long CurrentConcurrency => _tasks.Count;
        public long Found => _crawledUrls.Count;
        public long Errored => _errorCounter;
        public long Queued => _urls.Count;
        public long RobotDomains => _robotRestricted.Count;

        public int MaxConcurrency
        {
            get
            {
                return _maxConcurrency ?? 200;
            }
            set
            {
                ServicePointManager.DefaultConnectionLimit = value + 100;
                _maxConcurrency = value;
            }
        }

        public string LogFile => _logger.FilePath;

        public async Task<int> CrawlAsync(string startUrl)
        {
            _urls = new Stack<Uri>();
            _tasks = new List<Task<List<Uri>>>();
            _urls.Push(new Uri(startUrl));
            StartUrl = startUrl;


            while (_urls.Count > 0 || _tasks.Count > 0)
            {
                while (_tasks.Count < MaxConcurrency && _urls.Count > 0)
                {
                    _tasks.Add(CrawlAndProcessLink(_urls.Pop()));
                }

                Task t = await Task.WhenAny(Task.WhenAny(_tasks), Task.Delay(TimeSpan.FromMilliseconds(500)));

                var completed = _tasks.Where(x => x.Status == TaskStatus.RanToCompletion).ToList();
                foreach (var task in completed)
                {
                    _tasks.Remove(task);
                    foreach (var link in task.Result)
                    {
                        _urls.Push(link);
                    }
                }
            }

            return _crawledUrls.Count;
        }

        public async Task<string[]> RobotRestrictions(Uri url)
        {
            var response =
                await
                    _client.GetStringAsync(url.Scheme + Uri.SchemeDelimiter + url.Host + "/robots.txt")
                        .ConfigureAwait(false);
            return (from line in response.Split('\n')
                where line.StartsWith("Disallow:")
                let disallowed = line.Substring("Disallow:".Length).Trim()
                where !string.IsNullOrWhiteSpace(disallowed)
                select LogFoundDisallowedPrefix(disallowed, url)).ToArray();
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

        private async Task<List<Uri>> CrawlAndProcessLink(Uri url)
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
                        LogFoundLink(newLink.OriginalString);
                        _crawledUrls.Add(newLink.OriginalString);
                        returnLinks.Add(newLink);
                    }
                }
            }
            return returnLinks;
        }

        private void LogCrawlLink(string url)
        {
            Interlocked.Increment(ref _crawledCounter);
            _logger.Log($"Crawling:{url}");
        }

        private void LogFoundLink(string url)
        {
            _logger.Log($"Found:{url}");
        }

        private string LogFoundDisallowedPrefix(string disallowed, Uri url)
        {
            _logger.Log($"Disallowed:{url.Host}  Prefix:{disallowed}");
            return disallowed;
        }

        private void LogCrawlError(Exception exception, string url)
        {
            Interlocked.Increment(ref _errorCounter);
            _logger.Log($"CrawlError:{url} Exception:{exception}");
        }


        public async Task<List<Uri>> GetLinks(Uri url)
        {
            LogCrawlLink(url.OriginalString);
            var returnLinks = new List<Uri>();
            MatchCollection links;

            try
            {
                var response = await _client.GetStringAsync(url).ConfigureAwait(false);
                links = _linkRegex.Matches(response);
            }
            catch (Exception ex)
            {
                LogCrawlError(ex, url.OriginalString);
                return returnLinks;
            }


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