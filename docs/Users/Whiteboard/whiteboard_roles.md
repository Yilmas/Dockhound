# `/dock whiteboard roles`

**Description**

Set or update allowed roles for a MembersOnly whiteboard. This command requires **Manage Channels** permission.

**Usage**

- `/dock whiteboard roles <message_id>`

**Parameters**

- `message_id`: text — The message id for the whiteboard reference message.

**Permissions**

- **Manage Channels** required.

**Behavior**

- Presents a Role Select menu so the admin can pick one or more roles allowed to edit the whiteboard.
- Validates that the whiteboard exists and is in MembersOnly mode.
