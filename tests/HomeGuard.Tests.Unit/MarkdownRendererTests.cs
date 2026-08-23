using FluentAssertions;
using HomeGuard.Common.Text;
using Xunit;

namespace HomeGuard.Tests.Unit;

/// <summary>
/// The summary card hands its output straight to <c>MarkupString</c>, which does no
/// escaping of its own. Everything that keeps that safe lives in the renderer, so it is
/// worth pinning down rather than trusting.
/// </summary>
public sealed class MarkdownRendererTests
{
    // ── Ordinary rendering ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_renders_nothing(string? input)
        => MarkdownRenderer.ToHtml(input).Should().BeEmpty();

    [Fact]
    public void Markdown_becomes_html()
    {
        var html = MarkdownRenderer.ToHtml("**Покрытие**\n\n- ущерб\n- угон");

        html.Should().Contain("<strong>Покрытие</strong>");
        html.Should().Contain("<li>ущерб</li>");
    }

    [Fact]
    public void Pipe_tables_render()
    {
        var html = MarkdownRenderer.ToHtml("| a | b |\n|---|---|\n| 1 | 2 |");

        html.Should().Contain("<table>").And.Contain("<td>1</td>");
    }

    // ── Raw HTML ─────────────────────────────────────────────────────────────

    [Fact]
    public void Script_tags_are_escaped_rather_than_emitted()
    {
        var html = MarkdownRenderer.ToHtml("<script>alert('x')</script>");

        html.Should().NotContain("<script");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Inline_html_with_an_event_handler_is_escaped()
    {
        var html = MarkdownRenderer.ToHtml("<img src=x onerror=alert(1)>");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img");
    }

    // ── Link targets ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    public void Dangerous_link_schemes_are_neutralised(string url)
    {
        var html = MarkdownRenderer.ToHtml($"[клик]({url})");

        html.Should().Contain("""href="#" """.TrimEnd());
        html.Should().NotContain(url);
        html.Should().Contain("клик");          // текст ссылки остаётся видимым
    }

    [Fact]
    public void A_dangerous_image_source_is_neutralised()
    {
        var html = MarkdownRenderer.ToHtml("![картинка](javascript:evil)");

        html.Should().Contain("""src="#" """.TrimEnd());
        html.Should().NotContain("javascript:");
    }

    [Theory]
    [InlineData("https://example.com/policy.pdf")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+420123456789")]
    [InlineData("/docs/kasko.pdf")]
    [InlineData("#section")]
    [InlineData("./relative.pdf")]
    public void Safe_link_schemes_survive(string url)
    {
        var html = MarkdownRenderer.ToHtml($"[ссылка]({url})");

        html.Should().Contain(url);
    }

    [Fact]
    public void The_rewrite_does_not_produce_malformed_attributes()
    {
        var html = MarkdownRenderer.ToHtml("[клик](javascript:alert(1))");

        // Регрессия: raw string literal съедал закрывающую кавычку шаблона,
        // и на выходе получалось href="#"".
        html.Should().NotContain("\"\"");
    }
}
