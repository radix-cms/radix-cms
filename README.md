# Radix CMS

Radix CMS is an ASP.NET Core Razor Pages application. It provides a public
website with database-backed pages and an authenticated admin area for managing
content, users, themes, and media.

The current CMS application is in [`Radix/`](./Radix/). The separate
`node-server/` project is outside the scope of this README.

## Features

- Public Razor Pages and database-backed pages served by slug.
- Admin pages for content, media, themes, users, and site settings.
- Role-based access for Administrators, Managers, Editors, Registered users,
  and Unregistered visitors.
- MySQL/MariaDB storage for CMS pages and user accounts.
- Razor theme layouts with static assets served from `/themes/`.
- Custom .NET application features, including branch locations and a
  role-restricted project-password page.

## Requirements

- .NET 9 SDK.
- A MySQL or MariaDB server and a database containing the schema expected by
  Radix CMS.

The .NET application does not provide a database installer or create the CMS
schema on startup. Provision the database and an initial user account before
running the CMS.

## Run locally

From the solution root, configure the database connection, then run the .NET
project:

```powershell
$env:DB_HOST = "localhost"
$env:DB_PORT = "3306"
$env:DB_NAME = "radix"
$env:DB_USER = "your-database-user"
$env:DB_PASSWORD = "your-database-password"

dotnet run --project Radix\radix-cms.csproj
```

The application listens on port `5000` by default. Set `PORT` to use a
different port, for example:

```powershell
$env:PORT = "5080"
dotnet run --project Radix\radix-cms.csproj
```

Open the public site at `http://localhost:5000/` and the admin area at
`http://localhost:5000/admin`. Sign in at `/admin/login` with an account
already present in the CMS database.

Instead of the individual `DB_*` variables, you can provide a
`ConnectionStrings:Radix` connection string using the
`ConnectionStrings__Radix` environment variable. The individual connection
settings can also be supplied through the `Database` configuration section:
`Database__Host`, `Database__Port`, `Database__Name`, `Database__User`, and
`Database__Password`. Keep credentials in environment variables or another
local secret store; do not commit them to source control.

## Build and publish

Build the .NET CMS from the solution root:

```powershell
dotnet build Radix\radix-cms.csproj
```

Publish a Release deployment:

```powershell
dotnet publish Radix\radix-cms.csproj --configuration Release --output publish
```

Configure the database and `PORT` in the deployment environment before starting
the published application. The `Radix\Themes` assets and branch-location CSV
are included in build and publish output.

## CMS overview

### Pages and roles

Editors and higher roles can manage database-backed pages under
`/admin/pages`. Pages have a slug, content, publication status, publication
date, minimum access role, and selected theme. Published pages are available at
their slug when their publication date has arrived and the visitor meets the
page's minimum role. Static Razor Pages in `Radix/Pages/` are also used for
fixed routes and custom page experiences.

The roles, from least to most privileged, are:

```text
Unregistered < Registered < Editor < Manager < Administrator
```

User and site-settings management is restricted to Administrators. Editors and
higher roles can manage pages, media, and themes.

### Themes

Theme layouts are in `Radix/Themes/`. Each installed theme has a
`_Layout.cshtml` file; stylesheets and other theme assets are served under
`/themes/<theme-name>/`. See [THEMES.md](./THEMES.md) for layout and asset
details.

### Custom application features

Application-specific .NET code and its registration are in
`Radix/CustomCode/`. The current examples include the branch-locations API and
detail page, plus the Westbank project-password page. See
[`Radix/CustomCode/README.md`](./Radix/CustomCode/README.md) for configuration
and extension details.

## Documentation

- [Theme development](./THEMES.md)
- [Custom application code](./Radix/CustomCode/README.md)
- [Contributing](./CONTRIBUTING.md)
- [Security policy](./SECURITY.md)
- [Changelog](./CHANGELOG.md)

The guides under `Radix/Data/docs/` describe the separate Node CMS and are not
the operating instructions for this .NET application.
