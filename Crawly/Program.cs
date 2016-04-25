using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crawly
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var startUrl = args.Length > 0 ? args[0] : "https://www.google.com";

            int concurrency;
            var engine = new CrawlerEngine(new ResultsLogger(ConfigurationManager.AppSettings["Logfile"] ?? $".\\crawl.log"), int.TryParse(ConfigurationManager.AppSettings["MaxConcurrency"], out concurrency) ? concurrency : 20);
            var mainTasks = new List<Task>();
            var crawlTask = engine.CrawlAsync(startUrl);
            mainTasks.Add(crawlTask);
            if (string.Equals(ConfigurationManager.AppSettings["Interactive"] ?? "true", "false", StringComparison.OrdinalIgnoreCase))
            {
                mainTasks.Add(ConsoleAsync(engine, false));
            }
            else
            {
                mainTasks.Add(ConsoleAsync(engine, true));
                mainTasks.Add(InputAsync(engine));
            }

            
            Task.WaitAll(mainTasks.ToArray());
            Console.WriteLine($"Crawled {crawlTask.Result} pages");
        }

        private static async Task InputAsync(CrawlerEngine engine )
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

        private static async Task ConsoleAsync(CrawlerEngine engine , bool interactive)
        {
            var requestRate = new RollingAverage(40);
            var lastCrawled = 0l;
            var lastSample = DateTime.Now;
            Console.Clear();
            while (true)
            {
                if (interactive)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine($"Crawling from root:{engine.StartUrl} Logfile:{engine.LogFile}");
                }
                Console.WriteLine(
                    $"Crawled:{engine.Crawled.ToString("N0")}/{engine.Found.ToString("N0")}  Queued:{engine.Queued.ToString("N0").PadRight(8)}  Errored:{engine.Errored.ToString("N0"),6}  Request Rate:{(requestRate.GetAverage() / 1000f).ToString("N2").PadLeft(6)} r/s RobotDomains:{engine.RobotDomains.ToString().PadRight(6)} Concurrency:{engine.CurrentConcurrency.ToString().PadLeft(5)}/{engine.MaxConcurrency.ToString().PadRight(5)}"
                        .PadRight(Console.WindowWidth));
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                requestRate.AddSample((long)((engine.Crawled - lastCrawled) / ((DateTime.Now - lastSample).TotalMilliseconds / 1000000f)));
                lastCrawled = engine.Crawled;
                lastSample = DateTime.Now;
            }
        }

    }
}