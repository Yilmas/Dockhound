# `/dockadmin settings view`

**Description**

Download the current guild's saved settings as a JSON file. Useful for backups and manual inspection.

**Usage**

- `/dockadmin settings view`

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Retrieves the GuildConfig for the current guild and serializes it to JSON (pretty-printed), then sends it as a downloadable file attachment.
