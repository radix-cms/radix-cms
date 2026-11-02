# Contributing to Radix CMS

Thank you for your interest in contributing to Radix CMS.

## Before contributing

- Review the [README](README.md) for setup and project requirements.
- Review the [GNU GPL v3.0-or-later license](LICENSE).
- Do not include credentials, API keys, uploaded files, or other private data in
  issues, pull requests, or commits.

## Development

Install the .NET 9 SDK and configure a development database as described in the
[README](README.md). From the solution root, run the CMS project:

```powershell
dotnet run --project Radix\radix-cms.csproj
```

Before submitting a change, build the .NET CMS and confirm that it completes
successfully:

```powershell
dotnet build Radix\radix-cms.csproj
```

Include any relevant manual testing in your pull request description. Do not
commit credentials, local settings, uploaded media, or generated build output.

## Reporting issues

For support requests, please use [the support request form](https://ultraspark.net/request-support/)
or email [support@ultraspark.net](mailto:support@ultraspark.net).

When reporting a bug, include the Radix CMS version, .NET SDK version, database
engine/version, reproduction steps, and relevant logs. Remove secrets before
sharing logs.

## Pull requests

Describe the problem and solution, keep changes focused, and mention the
validation you performed.