# `/dock whiteboard mode`

**Description**

Change the privacy mode of an existing whiteboard. This command requires **Manage Channels** permission.

**Usage**

- `/dock whiteboard mode <message_id> <mode>`

**Parameters**

- `message_id`: text — The message id for the whiteboard reference message.
- `mode`: choice — AccessRestriction (Open | MembersOnly | Restricted).

**Permissions**

- The caller must have **Manage Channels** in the guild.

**Behavior**

- Finds the whiteboard by MessageId in the current guild and updates the Mode.
- If switching away from MembersOnly, clears the allowed roles list.
- If switching to MembersOnly, prompts the issuer with a Role Select menu to choose allowed roles.

**Notes**

- The command will attempt to update the persisted Info message that represents the whiteboard if present. If the info message is gone or not accessible, it will create a new message in the command channel.
