using Microsoft.AspNetCore.Razor.TagHelpers;

namespace dotnet_server.TagHelpers;

[HtmlTargetElement("rad-button")]
public sealed class RadButtonTagHelper(IConfiguration configuration) : TagHelper
{
    [HtmlAttributeName("url")]
    public string? Url { get; set; }

    [HtmlAttributeName("setting")]
    public string? Setting { get; set; }

    [HtmlAttributeName("title")]
    public string Title { get; set; } = string.Empty;

    [HtmlAttributeName("css-class")]
    public string CssClass { get; set; } = string.Empty;

    [HtmlAttributeName("new-window")]
    public bool NewWindow { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var href = string.IsNullOrWhiteSpace(Setting)
            ? Url
            : configuration[Setting];
        var classes = ("btn " + CssClass)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        output.TagName = "a";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("href", string.IsNullOrWhiteSpace(href) ? "#" : href);
        output.Attributes.SetAttribute("class", string.Join(' ', classes));
        if (NewWindow)
        {
            output.Attributes.SetAttribute("target", "_blank");
            output.Attributes.SetAttribute("rel", "noopener noreferrer");
        }

        output.Content.SetContent(Title);
    }
}
