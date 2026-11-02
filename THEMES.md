# .NET page themes

Radix CMS themes are Razor layouts stored under `Radix/Themes/`. Each installed
theme has a folder containing a `_Layout.cshtml` file. The `default` theme is
used when a page selects `default` or its requested theme is unavailable.

The current source includes the `default` and `landing` themes. Static Razor
pages select a layout by setting `Layout` in their `.cshtml` file. Database-
backed pages select a theme in the admin page editor; the `/` route may render
a database-backed page and use the theme selected for that page.

| Route | Theme |
| --- | --- |
| `/About` | default |
| `/gallery` | landing |
| `/hello` | default |
| `/services` | default |

## Creating a theme

Add a folder under `Radix/Themes/` with a `_Layout.cshtml` file. Theme names
must use lowercase letters, numbers, hyphens, or underscores. Layouts are
compiled with the application, so rebuild after adding or changing a layout.

The layout should contain `@RenderBody()` where the selected page content
should appear. It can also use `ViewData["Title"]` and normal Razor syntax.
Include `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` if the layout
uses ASP.NET Core tag helpers.

Put stylesheets, scripts, and images alongside `_Layout.cshtml`. They are
served under `/themes/<theme-name>/`. For example, an asset at
`Radix/Themes/marketing/style.css` can be loaded from
`/themes/marketing/style.css`.
