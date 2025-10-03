# Dockhound
Your friendly Companion Bot, for when you feel lonely and abandoned in the yard!

[![.NET](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml)
[![Docker](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml)
[![latest tag](https://badgen.net/github/tag/Yilmas/wll-tracker)](https://badgen.net/github/tag/Yilmas/wll-tracker)

## Current Features:
- Multi-Server support
- Yard Tracker
- Whiteboard
- Verification (auto-role manager)
	- Restrict channel
	- Request Recruit & Ally
- Bookmark


## WIP Features:
- Facilitate all Container Colors

## Notes
- While DotEnv are available with prefix `DOCK_`, I recommend using appsettings.json
- Applicant has been deprecated, code left behind outcommented for historical reasons (e.g., I might need some references later)

### Sample AppSettings

```json
{
  "Configuration": {
    "DiscordToken": "DISCORD_TOKEN",
    "DatabaseConnectionString": "SQL_DB_CONN",
    "AppInsightsConnectionString": "APP_INSIGHT",
    "Environment": "Development/Production"
  }
}

```