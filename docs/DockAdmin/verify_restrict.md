# `/dockadmin verify restrict`

**Description**

Set the verification access mode for the guild (Open / MembersOnly / Restricted). Also updates the saved info message if available.

**Usage**

- `/dockadmin verify restrict <setting>`

**Parameters**

- `setting`: choice — AccessRestriction (Open | MembersOnly | Restricted).

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Updates the stored restricted access level while preserving stored info message/channel ids.
- Attempts to update the persisted info message's embed to reflect the new setting. If the message or channel is missing or inaccessible, it will create a new info message in the command channel and save that reference.
- If setting MembersOnly, posts or updates an info message and may present a Role Select menu to choose allowed roles when no message exists.

**Notes**

- After updating the mode, a short ephemeral confirmation is sent to the admin.
