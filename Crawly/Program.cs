using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crawly
{
    class Program
    {
        static void Main(string[] args)
        {
            var startUrl = args.Length > 0 ? string.Join(" ", args) : "https://www.google.com";
            var engine = new CrawlerEngine();
            Task<int> t = engine.Crawl(startUrl);
            t.Wait();
            Console.WriteLine($"Crawled {t.Result} pages");
        }
    }
}
