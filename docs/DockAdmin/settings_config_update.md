# `/dockadmin settings config-update`

**Description**

Upload a GuildConfig JSON file to replace or update the guild's configuration.

**Usage**

- `/dockadmin settings config-update <file>`

**Parameters**

- `file`: file — A JSON file containing a valid GuildConfig structure.

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Validates the attachment size (must be >0 and <= 2 MB).
- Downloads and deserializes the JSON using case-insensitive property matching and allows numbers-as-strings for ulong values.
- Applies defensive defaults to avoid null references and sets the correct schema version if missing.
- Saves the configuration via the guild settings service and returns the saved configuration to the caller (as an attachment if large).
- Audits the configuration change to the configured safety/alerts channel if present by sending an embed describing the update.

**Error handling**

- Returns error messages for download failures, JSON deserialization errors, and persistence exceptions (including concurrency conflicts).
