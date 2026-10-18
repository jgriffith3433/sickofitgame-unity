using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SickOfIt.Tools.Editor;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The deploy's site content (GameDeployWindow step 7d, Tools/Web/site): copied pages and marked inserts.</summary>
    public class SiteContentTests
    {
        const string Index =
            "<main>\n    <section>\n      <h2 class=\"h3\">Latest Post</h2>\n      <div class=\"card\">old</div>\n    </section>\n" +
            "    <section>\n      <h2 class=\"h3\">Recent Posts</h2>\n      <div class=\"row\">\n        <div class=\"col\">a</div>\n      </div>\n    </section>\n</main>\n";

        static int Count(string html, string text) => Regex.Matches(html, Regex.Escape(text)).Count;

        [Test]
        public void AnInsertGoesAfterItsPlaceAndRunningItAgainReplacesIt()
        {
            string once = SiteContent.ApplyInsert(Index, "card", "<h2[^>]*>Recent Posts</h2>\\s*<div[^>]*>", null, "        <div class=\"col\">new</div>\n");
            Assert.That(once, Does.Contain("<div class=\"row\">\n<!-- Hawkes site: card -->\n        <div class=\"col\">new</div>\n<!-- /Hawkes site: card -->\n        <div class=\"col\">a</div>"));
            string twice = SiteContent.ApplyInsert(once, "card", "<h2[^>]*>Recent Posts</h2>\\s*<div[^>]*>", null, "        <div class=\"col\">newer</div>");
            Assert.That(Count(twice, "<!-- Hawkes site: card -->"), Is.EqualTo(1));
            Assert.That(twice, Does.Contain("newer"));
            Assert.That(twice, Does.Not.Contain(">new<"));
        }

        [Test]
        public void UntilReplacesWhatWasThere()
        {
            string html = SiteContent.ApplyInsert(Index, "latest", "<h2[^>]*>Latest Post</h2>", "</section>", "      <div class=\"card\">engine</div>");
            Assert.That(html, Does.Not.Contain(">old<"));
            Assert.That(html, Does.Contain("<!-- /Hawkes site: latest -->\n    </section>"));
            Assert.That(SiteContent.ApplyInsert(html, "latest", "<h2[^>]*>Latest Post</h2>", "</section>", "      <div class=\"card\">engine</div>"), Is.EqualTo(html));
        }

        [Test]
        public void AMissingPlaceIsReportedAndLineEndingsAreKept()
        {
            Assert.That(SiteContent.ApplyInsert(Index, "x", "<h2>Nowhere</h2>", null, "x"), Is.Null);
            string crlf = Index.Replace("\n", "\r\n");
            string html = SiteContent.ApplyInsert(crlf, "card", "<h2[^>]*>Recent Posts</h2>\\s*<div[^>]*>", null, "a\nb\n");
            Assert.That(Regex.IsMatch(html, "[^\r]\n"), Is.False, "every line ends in CRLF like the page");
        }

        [Test]
        public void TheManifestAndSnippetsStayOutOfTheWebsite()
        {
            Assert.That(SiteContent.IsCopied("blog/lore.html"), Is.True);
            Assert.That(SiteContent.IsCopied(@"blog\lore\img\band-X.webp"), Is.True);
            Assert.That(SiteContent.IsCopied("site.json"), Is.False);
            Assert.That(SiteContent.IsCopied("_inserts/blog-index-latest.html"), Is.False);
        }

        [Test]
        public void EveryInsertInTheRepoHasItsSnippet()
        {
            string folder = SiteContent.SourceFolder;
            var manifest = UnityEngine.JsonUtility.FromJson<SiteContent.SiteManifest>(File.ReadAllText(Path.Combine(folder, SiteContent.Manifest)));
            Assert.That(manifest.inserts, Is.Not.Empty);
            foreach (var insert in manifest.inserts)
            {
                Assert.That(File.Exists(Path.Combine(folder, insert.file)), insert.id + ": " + insert.file);
                Assert.That(insert.after, Is.Not.Empty, insert.id);
                Assert.DoesNotThrow(() => new Regex(insert.after), insert.id);
            }
        }
    }
}
