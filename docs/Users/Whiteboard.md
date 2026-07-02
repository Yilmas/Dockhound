# Whiteboard

?>  **Top-level invocation prefix:** `/dock whiteboard`

## Overview

Whiteboards let you create collaborative text boards inside a channel. A whiteboard is posted as a message with an embed showing its content, and includes action buttons to edit and view history.

**Examples**

1) Create an open whiteboard:

`/dock whiteboard create "Strategy Notes"`

2) Create a MembersOnly whiteboard and set roles:

`/dock whiteboard create "Officers Only" MembersOnly`

After creating in MembersOnly mode you must select the allowed roles in the follow-up role selector. If you wish to change these later you can run `/dock whiteboard roles <message_id>`.

3) Archive a whiteboard to prevent further edits:

`/dock whiteboard archive <message_id>`

- Archiving removes action components to prevent edits.

## Commands

* [Create](Users/Whiteboard?id=create): `/dock whiteboard create`
* [Mode](Users/Whiteboard?id=mode): `/dock whiteboard mode`
* [Roles](Users/Whiteboard?id=roles): `/dock whiteboard roles`
* [Info](Users/Whiteboard?id=info): `/dock whiteboard info`
* [Archive](Users/Whiteboard?id=archive): `/dock whiteboard archive`
* [List](Users/Whiteboard?id=list): `/dock whiteboard list`

---
### Create

**Description**

Create a new collaborative whiteboard in the current channel.

**Usage**

- `/dock whiteboard create <title> [mode]`

**Parameters**

- `title`: The title for the new whiteboard.
- `mode`: AccessRestriction (Open | MembersOnly | Restricted). Default: Open.

**Permissions**

- Any user can run this command to create a whiteboard in the current channel.
- If `MembersOnly` mode is selected, choosing allowed roles requires the creator to be able to edit the new whiteboard. Users with channel-level **Manage Channel** or **Pin Messages** can do this immediately. First-time `MembersOnly` setup should usually be done by one of those users, because no allowed roles exist yet.

**Behavior**

- Creates a Whiteboard entity persisted to the database.
- Seeds an initial empty version (v1) and posts an embed message in the channel containing the whiteboard.
- If `MembersOnly` is selected and the creator has editor access, a role select menu is presented in a follow-up to pick allowed roles.

---
### Mode

**Description**

Change the restriction mode of an existing whiteboard.

**Usage**

- `/dock whiteboard mode <message_id> <mode>`

**Parameters**

- `message_id`: The message id for the whiteboard reference message.
- `mode`: AccessRestriction (Open | MembersOnly | Restricted).

**Permissions**

- The caller must be able to edit the whiteboard.
- Users with channel-level **Manage Channel** or **Pin Messages** can edit any whiteboard in that channel.
- `Open` whiteboards can be edited by any guild user.
- `MembersOnly` whiteboards can be edited by users with one of the allowed roles.
- `Restricted` whiteboards can only be edited by users with channel-level **Manage Channel** or **Pin Messages**.

**Behavior**

- Finds the whiteboard by MessageId in the current guild and updates the Mode.
- If switching away from MembersOnly, clears the allowed roles list.
- If switching to MembersOnly, prompts the issuer with a Role Select menu to choose allowed roles.

---
### Roles

**Description**

Set or update allowed roles for a MembersOnly whiteboard.

**Usage**

- `/dock whiteboard roles <message_id>`

**Parameters**

- `message_id`: The message id for the whiteboard reference message.

**Permissions**

- The caller must be able to edit the whiteboard.
- Users with channel-level **Manage Channel** or **Pin Messages** can edit any whiteboard in that channel.
- `MembersOnly` whiteboards can be edited by users with one of the allowed roles.
- `Open` whiteboards can be edited by any guild user, but this command only applies when the target whiteboard is currently `MembersOnly`.

**Behavior**

- Presents a Role Select menu so the user can pick one or more roles allowed to edit the whiteboard.
- Validates that the whiteboard exists and is in MembersOnly mode.

---
### Info

**Description**

Show details about a whiteboard, including allowed roles, creator, latest version, and whether it's archived.

**Usage**

- `/dock whiteboard info <message_id>`

**Parameters**

- `message_id`: The message id for the whiteboard.

**Permissions**

- Any user can run this command to view details.
- Recent edit history is only included when the caller can edit the whiteboard.
- Users with channel-level **Manage Channel** or **Pin Messages** can see recent edit history for any whiteboard in that channel.
- For `Open` whiteboards, any guild user can see recent edit history.
- For `MembersOnly` whiteboards, users with one of the allowed roles can see recent edit history.
- For `Restricted` whiteboards, only users with channel-level **Manage Channel** or **Pin Messages** can see recent edit history.

**Behavior**

- Retrieves the whiteboard and versions, builds an embed with fields: Title, Channel, Mode, Allowed Roles, Created, Archived, Latest Version.
- If the caller can edit the whiteboard, additional recent edit history is included.

---
### Archive

**Description**

Toggle archive state on a whiteboard (close or reopen it).

**Usage**

- `/dock whiteboard archive <message_id>`

**Parameters**

- `message_id`: The message id for the whiteboard.

**Permissions**

- The caller must have channel-level **Manage Channel** or **Pin Messages** on the whiteboard channel.

**Behavior**

- Toggles the `IsArchived` flag and updates the whiteboard title with a lock emoji when archived.
- Updates the persisted message embed and removes action components when archived (to prevent further edits).

---
### List

**Description**

List all whiteboards in the current channel.

**Usage**

- `/dock whiteboard list`

**Permissions**

- Any user may run this command inside a guild channel.

**Behavior**

- Queries the database for whiteboards in the channel and validates that their referenced message still exists.
- Marks whiteboards as deleted (`IsDeleted`) if their message or channel is missing or inaccessible.
- Returns a list of whiteboards with links to their messages if available. If the output is too long, it is returned as a text file attachment.
