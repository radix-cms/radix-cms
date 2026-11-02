# Custom application code

Put web-application-specific .NET code in this folder instead of mixing it
with Radix CMS services and routes. This code is compiled into the server; it
is not a separately installed or dynamically loaded plugin.

Register custom services through `AddCustomCode` and custom endpoints through
`MapCustomCode` in `CustomCodeExtensions.cs`. Keep application-specific Razor
pages under `Pages/CustomPages/`, and put feature-specific assets alongside
their owning application or theme.

The branch locations feature is an example: its service and API endpoint live
here, and its detail page is under `Pages/CustomPages/Locations/`. The Westbank
project-password feature is also registered here; its manager/administrator
page is under `Pages/CustomPages/`.

The Westbank feature uses the separate SQL Server configuration section
`mssql_intranet`, with `server`, `database`, `user`, and `password` values.
Optional `port` and `options.encrypt` / `options.trustServerCertificate`
settings are supported. The .NET server reads this section from its normal
configuration first, then `appsettings.local.json`, then the legacy
`node-server/appsettings.json` fallback when present. Environment variables
and command-line arguments take precedence. The local settings file is ignored
by Git and can contain:

```json
{
  "mssql_intranet": {
    "server": "your-sql-server",
    "port": 1433,
    "database": "your-database",
    "user": "your-database-user",
    "password": "your-database-password",
    "options": {
      "encrypt": false,
      "trustServerCertificate": true
    }
  }
}
```

Alternatively, set the values outside source control with .NET user secrets
or environment variables such as `mssql_intranet__server` and
`mssql_intranet__password`.

Add further application-specific features here as they are needed. Keep
shared CMS behavior, including authentication, administration, and
database-backed CMS pages, in the existing Radix code.
