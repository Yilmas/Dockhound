# `/dock whiteboard info`

**Description**

Show details about a whiteboard, including allowed roles, creator, latest version, and whether it's archived.

**Usage**

- `/dock whiteboard info <message_id>`

**Parameters**

- `message_id`: text — The message id for the whiteboard.

**Permissions**

- Any user can run this command to view details.

**Behavior**

- Retrieves the whiteboard and versions, builds an embed with fields: Title, Channel, Mode, Allowed Roles, Created, Archived, Latest Version.
- If the caller has **Manage Channels**, additional recent edit history is included.
