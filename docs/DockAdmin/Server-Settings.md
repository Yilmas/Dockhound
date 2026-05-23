# Server Settings

?>  **Top-level invocation prefix:** `/dockadmin settings`

## Overview

Use these commands to configure Dockhound for a Discord server.

### Installing Dockhound on a New Server

When Dockhound is added to a new server, a server admin should configure it in this order:

1. Run `/dockadmin settings guild-update [name] [tag]`.
   - This sets the server name and tag used by Dockhound.
   - This also initializes the guild in the database. Without this first step, later configuration commands will fail because Dockhound has no saved guild record to attach settings to.
2. Run `/dockadmin settings view`.
   - This returns a fresh JSON template for the server configuration.
   - Use this file as the starting point for the server's Dockhound settings.
3. Edit the downloaded JSON file.
   - Fill in the required role ids, channel ids, verification settings, and any other server-specific values.
4. Run `/dockadmin settings config-update <file>`.
   - Upload the edited JSON file to save the server configuration.
   - Dockhound returns the saved configuration so the admin can confirm what was stored.

## View

**Description**

Download the current guild's saved settings as a JSON file. Useful for backups and manual inspection.

**Usage**

- `/dockadmin settings view`

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Retrieves the GuildConfig for the current guild and serializes it to JSON (pretty-printed), then sends it as a downloadable file attachment.

## Guild Update

**Description**

Update the stored guild name and/or short tag used by the bot's internal data.

**Usage**

- `/dockadmin settings guild-update [name] [tag]`

**Parameters**

- `name`: text (optional) New display name for the guild.
- `tag`: text (optional) Short identifier tag for the guild.

**Permissions**

- Requires **Administrator** permission.

## Config Update

**Description**

Upload a GuildConfig JSON file to update the guild's configuration.

**Usage**

- `/dockadmin settings config-update <file>`

**Parameters**

- `file`: A JSON file containing a valid GuildConfig structure.

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Applies defensive defaults to avoid null references and sets the correct schema version if missing.
- Audits the configuration change to the configured safety/alerts channel if present by sending an embed describing the update.

**Error handling**

- Returns error messages for download failures, JSON deserialization errors, and persistence exceptions (including concurrency conflicts).

## Logs by Message

**Description**

Show all log events for a given message id over a specified timespan in days. Groups results by ISO week and displays counts per week.

**Usage**

- `/dockadmin settings logs-per-message <message_id> <days_span>`

**Parameters**

- `message_id`: Discord message ID to look up.
- `days_span`: Days into the past to include (positive number). The command internally negates it to compute the cutoff.

**Permissions**

- Requires **View Audit Log** and **Manage Messages** permissions.

**Behavior**

- Validates the message id format and computes a cutoff date.
- Queries the LogEvents table for matches on MessageId and Updated >= cutoff.
- Groups events by ISO year+week and renders weekly lines, including weeks with zero updates.
- Returns an embed showing total events, weeks covered, and a code block with weekly updates.

**Notes**

- Uses ISO week handling to present consistent weekly buckets starting on Monday.
