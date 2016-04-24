using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crawly
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var startUrl = args.Length > 0 ? string.Join(" ", args) : "https://www.google.com";
            var engine = new CrawlerEngine(new ResultsLogger($".\\crawl.log"), 500);
            var crawlTask = engine.CrawlAsync(startUrl);
            var consoleTask = ConsoleAsync(engine);
            var inputTask = InputAsync(engine);
            Task.WaitAll(consoleTask, crawlTask, inputTask);
            Console.WriteLine($"Crawled {crawlTask.Result} pages");
        }

        private static async Task InputAsync(CrawlerEngine engine)
        {
            await Task.Factory.StartNew(() =>
            {
                var key = Console.ReadKey().Key;
                var stepsize = 10;
                while (key != ConsoleKey.Q)
                {
                    if (key == ConsoleKey.UpArrow)
                    {
                        engine.MaxConcurrency += stepsize;
                    }
                    else if (key == ConsoleKey.DownArrow)
                    {
                        engine.MaxConcurrency = engine.MaxConcurrency - stepsize > 0
                            ? engine.MaxConcurrency - stepsize
                            : 0;
                    }
                    else if (key == ConsoleKey.LeftArrow)
                    {
                        stepsize -= 10;
                    }
                    else if (key == ConsoleKey.RightArrow)
                    {
                        stepsize += 10;
                    }
                    key = Console.ReadKey().Key;
                }
                Environment.Exit(0);
            });
        }

        private static async Task ConsoleAsync(CrawlerEngine engine )
        {
            var requestRate = new RollingAverage(8);
            var lastCrawled = 0l;
            var lastSample = DateTime.Now;

            while (true)
            {
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Crawling from root:{engine.StartUrl} Logfile:{engine.LogFile}");
                Console.WriteLine(
                    $"Crawled:{engine.Crawled.ToString().PadRight(10)}  Queued:{engine.Queued.ToString().PadRight(8)} Request Rate:{requestRate.GetAverage().ToString().PadLeft(6)} r/s RobotDomains:{engine.RobotDomains.ToString().PadRight(6)} Concurrency:{engine.CurrentConcurrency.ToString().PadLeft(5)}/{engine.MaxConcurrency.ToString().PadRight(5)}"
                        .PadRight(Console.WindowWidth));
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                requestRate.AddSample((long)((engine.Crawled - lastCrawled) / ((DateTime.Now - lastSample).TotalMilliseconds / 1000)));
                lastCrawled = engine.Crawled;
                lastSample = DateTime.Now;
            }
        }

    }
}