using System.Text.RegularExpressions;
using Markdig;

namespace HomeGuard.Common.Text;

/// <summary>
/// Turns the household's markdown into HTML that is safe to hand to <c>MarkupString</c>.
/// <para>
/// Two defences, both cheap. The pipeline runs with <c>DisableHtml</c>, so raw HTML in
/// the source is escaped and rendered as text rather than executed — that removes the
/// script-tag route entirely without pulling in a sanitiser library. What that does not
/// cover is a markdown link carrying a <c>javascript:</c> URL, which Markdig will happily
/// emit as an <c>href</c>, so link and image targets are checked against a scheme
/// allowlist afterwards.
/// </para>
/// <para>
/// The threat model here is modest — the only authors are the two adults in the house —
/// but a note pasted from a website is a real way for something odd to arrive, and the
/// cost of closing it is a few lines.
/// </para>
/// </summary>
public static partial class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseTaskLists()
        .Build();

    public static string ToHtml(string? markdown)
        => string.IsNullOrWhiteSpace(markdown)
            ? string.Empty
            : NeutraliseUnsafeUrls(Markdown.ToHtml(markdown, Pipeline));

    /// <summary>
    /// Rewrites any href/src that is not http(s), mailto, a fragment or a relative path.
    /// Rewriting rather than removing keeps the link text visible — the reader still sees
    /// that something was linked, it just does not go anywhere.
    /// </summary>
    private static string NeutraliseUnsafeUrls(string html)
        => UrlAttribute().Replace(html, match =>
        {
            var attribute = match.Groups["attr"].Value;
            var url       = match.Groups["url"].Value.Trim();

            return IsSafe(url) ? match.Value : $"{attribute}=\"#\"";
        });

    private static bool IsSafe(string url)
    {
        if (url.Length == 0) return true;
        if (url.StartsWith('#') || url.StartsWith('/') || url.StartsWith('.')) return true;

        var colon = url.IndexOf(':');
        if (colon < 0) return true;   // relative path with no scheme

        var scheme = url[..colon].ToLowerInvariant();
        return scheme is "http" or "https" or "mailto" or "tel";
    }

    // Обычная строка с экранированием, а не raw string: в raw-варианте закрывающая
    // кавычка шаблона съедается ограничителем, и из замены выходит href="#"".
    [GeneratedRegex("(?<attr>href|src)\\s*=\\s*\"(?<url>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex UrlAttribute();
}
