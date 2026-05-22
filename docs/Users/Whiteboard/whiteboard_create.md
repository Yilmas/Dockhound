# `/dock whiteboard create`

**Description**

Create a new collaborative whiteboard in the current channel.

**Usage**

- `/dock whiteboard create <title> [mode]`

**Parameters**

- `title`: text — The title for the new whiteboard.
- `mode`: choice — AccessRestriction (Open | MembersOnly | Restricted). Default: Open.

**Permissions**

- Any user can run this command to create a whiteboard. If you choose MembersOnly mode, the command will prompt for roles — setting allowed roles requires **Manage Channels** permission.

**Behavior**

- Creates a Whiteboard entity persisted to the database.
- Seeds an initial empty version (v1) and posts an embed message in the channel containing the whiteboard. The bot saves the posted message id.
- If MembersOnly is selected and the invoker lacks Manage Channels, the bot notifies that Manage Channels is required to set roles.
- If the invoker has Manage Channels and chosen MembersOnly, a role select menu is presented in a follow-up to pick allowed roles.

**Interactive components**

- The created whiteboard message includes action buttons and a history button (unless archived).
- When MembersOnly, a Role Select menu is used to choose allowed roles.
