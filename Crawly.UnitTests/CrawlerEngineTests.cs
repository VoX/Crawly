 using System.Linq;
 using NUnit.Framework;

namespace Crawly.UnitTests
{
    [TestFixture]
    public class CrawlerEngineTests
    {
        [Test]
        public void GetLinks_ReturnsLinks()
        {
            var engine = new CrawlerEngine();
            var links = engine.GetLinks("https://www.penny-arcade.com/comic").Result;
            Assert.True(links.Any(x=>x.Contains("http://paxsite.com/")), "http://paxsite.com/");
            Assert.True(links.Any(x => x.Contains("http://forums.penny-arcade.com/")), "http://forums.penny-arcade.com/");
            Assert.True(links.Any(x => x.Contains("https://www.penny-arcade.com/clubpa")), "/clubpa");
            Assert.False(links.Any(x => x.Contains("https://www.penny-arcade.com/archive/results/search")), "https://www.penny-arcade.com/archive/results/search");
        }

        [Test]
        public void Crawl_Debuglol()
        {
            var engine = new CrawlerEngine();
            var x = engine.Crawl("http://www.google.com").Result;
        }
    }
}