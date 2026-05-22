# `/dockadmin settings guild-update`

**Description**

Update the stored guild name and/or short tag used by the bot's internal data.

**Usage**

- `/dockadmin settings guild-update [name] [tag]`

**Parameters**

- `name`: text (optional) New display name for the guild.
- `tag`: text (optional) Short identifier tag for the guild.

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Validates that at least one of name or tag is provided.
- Finds the Guild record in the database and updates the Name and/or Tag fields, saving changes.
- Returns an embedded confirmation showing the updated values.

**Notes**

- If no Guild record exists (e.g., config-update wasn't run), the command prompts the admin to run config-update first.
