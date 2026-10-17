using System.Text.RegularExpressions;
using NUnit.Framework;
using SickOfIt.Tools.Editor;
using Placement = SickOfIt.Tools.Editor.GameDeployWindow.CreditPlacement;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The deploy's "Powered by HAWKES GEN" mark on the website's pages (GameDeployWindow step 7c).</summary>
    public class HawkesCreditTests
    {
        const string BlogPage =
            "<html>\n<body>\n  <main>Post</main>\n\n  <footer class=\"bg-black\">\n    <p>&copy; 2026 Sick Of It.</p>\n  </footer>\n\n  <script src=\"x.js\"></script>\n</body>\n</html>\n";

        const string Landing =
            "<html>\r\n  <body>\r\n    <div id=\"start-overlay\"><a href=\"/Game/index.html\">CLICK TO START</a></div>\r\n  </body>\r\n</html>\r\n";

        static int Count(string html, string text) => Regex.Matches(html, Regex.Escape(text)).Count;

        [Test]
        public void EveryPageButTheGameGetsTheMark()
        {
            Assert.That(GameDeployWindow.IsSitePage("index.html"), Is.True);
            Assert.That(GameDeployWindow.IsSitePage("blog/lore/characters/overlord.html"), Is.True);
            Assert.That(GameDeployWindow.IsSitePage(@"backend\templates\gate.html"), Is.True);
            Assert.That(GameDeployWindow.IsSitePage("Game/index.html"), Is.False);
            Assert.That(GameDeployWindow.IsSitePage("Game/v107/SickOfIt/index.html"), Is.False);
            Assert.That(GameDeployWindow.IsSitePage("blog/emoji-reactions.js"), Is.False);
            Assert.That(GameDeployWindow.CreditPlacementFor("index.html"), Is.EqualTo(Placement.Landing));
            Assert.That(GameDeployWindow.CreditPlacementFor("blog/index.html"), Is.EqualTo(Placement.Footer));
        }

        [Test]
        public void BlogPagesGetItInTheirFooter()
        {
            string html = GameDeployWindow.InstallCredit(BlogPage, Placement.Footer);
            Assert.That(Count(html, "href=\"https://hawkesgen.com\""), Is.EqualTo(1));
            Assert.That(html, Does.Contain("Powered by <strong>HAWKES GEN</strong>"));
            Assert.That(html, Does.Not.Contain("hawkesbiomedical.com"));
            int mark = html.IndexOf("hawkesgen.com", System.StringComparison.Ordinal);
            Assert.That(mark, Is.GreaterThan(html.IndexOf("<footer", System.StringComparison.Ordinal)));
            Assert.That(mark, Is.LessThan(html.IndexOf("</footer>", System.StringComparison.Ordinal)));
        }

        [Test]
        public void APageWithoutAFooterGetsABar()
        {
            string page = BlogPage.Replace("<footer class=\"bg-black\">\n    <p>&copy; 2026 Sick Of It.</p>\n  </footer>\n", "");
            string html = GameDeployWindow.InstallCredit(page, Placement.Footer);
            Assert.That(html, Does.Contain("hawkes-credit-bar"));
            Assert.That(html.IndexOf("hawkesgen.com", System.StringComparison.Ordinal), Is.LessThan(html.IndexOf("</body>", System.StringComparison.Ordinal)));
        }

        [Test]
        public void TheLandingPageAlsoLinksTheParentCompanyAndKeepsItsLineEndings()
        {
            string html = GameDeployWindow.InstallCredit(Landing, Placement.Landing);
            Assert.That(Count(html, "href=\"https://hawkesgen.com\""), Is.EqualTo(1));
            Assert.That(Count(html, "href=\"https://hawkesbiomedical.com\""), Is.EqualTo(1));
            Assert.That(html, Does.Contain("hawkes-credit-pinned"));
            Assert.That(Regex.IsMatch(html, "[^\r]\n"), Is.False, "every line ends in CRLF like the page");
        }

        [Test]
        public void RunningItAgainChangesNothing()
        {
            string once = GameDeployWindow.InstallCredit(BlogPage, Placement.Footer);
            Assert.That(GameDeployWindow.InstallCredit(once, Placement.Footer), Is.EqualTo(once));
            string landing = GameDeployWindow.InstallCredit(Landing, Placement.Landing);
            Assert.That(GameDeployWindow.InstallCredit(landing, Placement.Landing), Is.EqualTo(landing));
            Assert.That(GameDeployWindow.InstallCredit("<html>no body</html>", Placement.Footer), Is.Null);
        }
    }
}
