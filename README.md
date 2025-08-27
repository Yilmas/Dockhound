# Dockhound
Your friendly Companion Bot, for when you feel lonely and abandoned in the yard!

[![.NET](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml)
[![Docker](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml)
[![latest tag](https://badgen.net/github/tag/Yilmas/wll-tracker)](https://badgen.net/github/tag/Yilmas/wll-tracker)

## Current Features:
- Yard Tracker
- Whiteboard
- Verification (auto-role manager)
	- Restrict channel
	- Request Recruit & Ally
- Bookmark


## WIP Features:
- Multi-Server support
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
  },
  "Verify": {
    "ImageUrl": "URL_VERIFY_IMAGE",
    "ReviewChannelId": CHANNEL_ID_ULONG,
    "NotificationChannelId": CHANNEL_ID_ULONG,
    "ColonialSecureChannelId": CHANNEL_ID_ULONG,
    "WardenSecureChannelId": CHANNEL_ID_ULONG,
    "RecruitAssignerRoles": [
      ROLE_ID
    ],
    "AllyAssignerRoles": [
      ROLE_ID
    ],
    "RestrictedAccess": {
      "AlwaysRestrictRoles": [
        ROLE_ID,
        ROLE_ID
      ],
      "MemberOnlyRoles": [
        ROLE_ID
      ],
      "ChannelId": CHANNEL_ID_ULONG,
      "MessageId": MESSAGE_ID_ULONG
    }
  },
  "Applicant": {
    "ForumChannelId": FORUM_CHANNEL_ID,
    "PendingTagChannelId": CHANNEL_ID_ULONG,
    "AllowedAssignerRoleIds": [
      ROLE_ID,
      ROLE_ID,
      ROLE_ID
    ]
  }
}

```