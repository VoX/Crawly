 using System;
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
            var links = engine.GetLinks(new Uri("https://www.penny-arcade.com/comic")).Result;
            Assert.True(links.Any(x=>x.OriginalString.Contains("http://paxsite.com/")), "http://paxsite.com/");
            Assert.True(links.Any(x => x.OriginalString.Contains("http://forums.penny-arcade.com/")), "http://forums.penny-arcade.com/");
            Assert.True(links.Any(x => x.OriginalString.Contains("https://www.penny-arcade.com/clubpa")), "/clubpa");
            Assert.False(links.Any(x => x.OriginalString.Contains("https://www.penny-arcade.com/archive/results/search")), "https://www.penny-arcade.com/archive/results/search");
        }

        [Test]
        public void DisallowedLinks()
        {
            var engine = new CrawlerEngine();
            var links = engine.RobotRestrictions(new Uri("https://www.penny-arcade.com/comic")).Result;
            Assert.True(links.Any(x => x == "/feed/"), "/feed/");
        }

        [Test]
        public void IsRobotRestricted()
        {
            var engine = new CrawlerEngine();
            Assert.False(engine.IsRobotRestricted(new Uri("https://www.penny-arcade.com/comic")).Result);
            Assert.True(engine.IsRobotRestricted(new Uri("https://www.penny-arcade.com/feed/test")).Result);
        }
    }
}