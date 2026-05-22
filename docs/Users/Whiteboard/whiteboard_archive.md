# `/dock whiteboard archive`

**Description**

Toggle archive state on a whiteboard (close or reopen it). This command requires **Manage Channels** permission.

**Usage**

- `/dock whiteboard archive <message_id>`

**Parameters**

- `message_id`: The message id for the whiteboard.

**Permissions**

- **Manage Channels** required.

**Behavior**

- Toggles the `IsArchived` flag and updates the whiteboard title with a lock emoji when archived.
- Updates the persisted message embed and removes action components when archived (to prevent further edits).
