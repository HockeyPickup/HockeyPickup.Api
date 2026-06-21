# 🏒 HockeyPickup.Api

[![Logo](static/JB_Puck_Logo.png)](https://hockeypickup.com)

[![HockeyPickup.Api](https://github.com/HockeyPickup/HockeyPickup.Api/actions/workflows/master_hockeypickupapi.yml/badge.svg)](https://github.com/HockeyPickup/HockeyPickup.Api/actions/workflows/master_hockeypickupapi.yml)
[![Coverage Status](https://coveralls.io/repos/github/HockeyPickup/HockeyPickup.Api/badge.svg)](https://coveralls.io/github/HockeyPickup/HockeyPickup.Api)
[![CodeQL](https://github.com/HockeyPickup/HockeyPickup.Api/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/HockeyPickup/HockeyPickup.Api/actions/workflows/github-code-scanning/codeql)

## 🌈 Overview

HockeyPickup.Api is the core backend for [HockeyPickup](https://hockeypickup.com).

The main technology stack platform is [.NET Core](https://dotnet.microsoft.com/) 10.0.

## 🎟️ Lottery Queuing

Sessions can fill spots by lottery instead of first-come buying. When a tier's buy window opens, an entry window collects entrants; at the draw time they are shuffled into a random order and the normal buy flow runs for each in turn. Draws are scheduled per tier via Service Bus and executed by a background consumer, with a daily safety-net sweep (`POST /api/lottery/execute-due`) to recover any missed draw. Each session toggles this with `LotteryEnabled`; when off, buying is first-come.

## 🛠 Prerequisites

* Install Visual Studio 2026 or later, or Visual Studio Code. Ensure that `$ dotnet --version` is at least 10.0.

## ⌨️ Install, Build, and Serve the Site

Create a new file at the root of the HockeyPickup.Api project named `appsettings.json` with the following contents:

1. Get the `<DatabaseConnectionString>` from a local instance of SQL Server.

2. Get the `<ServiceBusConnectionString>` from Azure portal. Currently Service Bus is not available to run locally.

3. Get the `<AzureStorage>` from Azure Storage Manager emulator.

4. `<InviteCode>` can be any string, required to register a new account.

5. Create a `<JWTBase64Key>`: `$ openssl rand -base64 32`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.AspNetCore.Mvc.ApiExplorer": "Debug"
    },
    "Console": {
      "IncludeScopes": true,
      "LogLevel": {
        "Default": "Debug",
        "System": "Information",
        "Microsoft": "Information",
        "Microsoft.AspNetCore.Mvc.ApiExplorer": "Debug"
      }
    }
  },
  "AllowedHosts": "*",
  "WEBSITE_CONTENTAZUREFILESCOMPATIBILITYOVERRIDE": 1,
  "ConnectionStrings": {
    "DefaultConnection": "<DatabaseConnectionString>",
    "ServiceBusConnectionString": "<ServiceBusConnectionString>",
    "AzureStorage": "<AzureStorage>"
  },
  "JwtSecretKey": "<JWTSecretKey>",
  "JwtIssuer": "HockeyPickupApi",
  "JwtAudience": "https://api.hockeypickup.com/api/",
  "ServiceBusCommsQueueName": "comms-dev",
  "ServiceBusHealthCheckQueueName": "health-dev",
  "RegistrationInviteCode": "<InviteCode>",
  "SessionBuyPrice": "27.00",
  "SiteTitle": "Hockey Pickup",
  "RinkLocation": "Toyota Sports Performance Center, 555 N Nash Street, El Segundo, CA 90245",
  "BaseUrl": "https://localhost:5174"
}
```

### Install the packages

```bash
$ dotnet restore
$ dotnet tool restore
```
Open HockeyPickup.Api.sln solution in Visual Studio, and build the solution.

You'll see output in the console showing the various local URL access points.

Swagger root [`https://localhost:7042/index.html`](https://localhost:7042/swagger/index.html)

GraphQL root [`https://localhost:7042/api/graphql`](https://localhost:7042/api/graphql)

## 🧪 Unit Testing

Unit testing and code coverage are setup and **must** be maintained. To run the tests and generate a coverage report, run the Powershell script from the command line.

```bash
$ powershell ./scripts/RunTests.ps1
```

This generates a coverage report in `HockeyPickup.Api.Tests/coverage-html`. Open `index.html` to view the report.

## 📮 Making requests via Postman

[Postman](https://www.postman.com/) is a useful tool for testing Apis.

## 🎁 Versioning

HockeyPickup.Api uses [sementic versioning](https://semver.org/), starting with 1.0.0.

The patch (last segment of the 3 segments) is auto-incremented via a GitHub action when a pull request is merged to master. The GitHub action is configured in [.github/workflows/hockeypickup-api-version.yml](.github/workflows/hockeypickup-api-version.yml). To update the major or minor version, follow the instructions specified in the [bumping section of the action](https://github.com/anothrNick/github-tag-action#bumping) - use #major or #minor in the commit message to auto-increment the version.

## ❤️ Contributing

We welcome useful contributions. Please read our [contributing guidelines](CONTRIBUTING.md) before submitting a pull request.

## 📜 License

HockeyPickup.Api is licensed under the MIT license.

[![License](https://img.shields.io/github/license/HockeyPickup/HockeyPickup.Api)]((https://github.com/HockeyPickup/HockeyPickup.Api/master/LICENSE))

[hockeypickup.com](https://hockeypickup.com)
<!---
Icons used from: https://emojipedia.org/
--->