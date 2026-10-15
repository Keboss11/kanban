using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace Kanban.Web.Services;

/// <summary>
/// Markdown → HTML seguro: sin HTML crudo y solo enlaces http(s), mailto o relativos.
/// El contenido puede venir de una IA vía MCP, así que se trata como no confiable.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkupString("");

        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
                link.Url = "#";
        }
        return new MarkupString(document.ToHtml(Pipeline));
    }

    internal static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;
        if (!Uri.TryCreate(url.Trim(), UriKind.RelativeOrAbsolute, out var uri))
            return false;
        return !uri.IsAbsoluteUri || uri.Scheme is "http" or "https" or "mailto";
    }
}
