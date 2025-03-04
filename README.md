# WLL Tracker
A bot for the Foxhole regiment Winter Legion. 

[![.NET](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/dotnet.yml)
[![Docker](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml/badge.svg)](https://github.com/Yilmas/wll-tracker/actions/workflows/release.yaml)

## Current Features:
- Yard Tracker
- Whiteboard
- Verification (auto-role manager)

## Usage
Setup .env variables as following:

- `WLL_TOKEN={Discord Token}`
- `WLL_DBCONN={MSSQL Connection String}`
- `WLL_ENVIRONMENT=Production`
- `WLL_CHANNEL_VERIFY_REVIEW={Channel for submission to be sent}`
- `WLL_CHANNEL_VERIFY_NOTIFICATION={Channel for approvals to be announced}`
- `WLL_CHANNEL_FACTION_COLONIAL_SECURE={Recommended Channel for Colonial}`
- `WLL_CHANNEL_FACTION_WARDEN_SECURE={Recommended Channel for Warden}`
