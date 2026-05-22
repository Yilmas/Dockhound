# Whiteboard — Overview & Examples

**Top-level invocation prefix:** `/dock whiteboard`

## Overview

Whiteboards let you create collaborative text boards inside a channel. A whiteboard is posted as a message with an embed showing its content, and includes action buttons to edit, view history, and other controls.

## Key commands

- **Create:** `/dock whiteboard create <title> [mode]` — create a new whiteboard in the current channel.
- **Mode:** `/dock whiteboard mode <message_id> <mode>` — change privacy mode (Manage Channels required).
- **Roles:** `/dock whiteboard roles <message_id>` — set allowed roles (Manage Channels required for MembersOnly mode).
- **Info:** `/dock whiteboard info <message_id>` — show details about a whiteboard.
- **Archive:** `/dock whiteboard archive <message_id>` — archive/unarchive (Manage Channels required).
- **List:** `/dock whiteboard list` — list whiteboards in this channel.

## Examples

1) Create an open whiteboard:

`/dock whiteboard create "Strategy Notes"`

2) Create a MembersOnly whiteboard and set roles:

`/dock whiteboard create "Officers Only" MembersOnly`

After creating in MembersOnly mode, run `/dock whiteboard roles <message_id>` or use the follow-up role select menu to pick allowed roles.

3) Archive a whiteboard to prevent further edits:

`/dock whiteboard archive <message_id>`

## Notes

- Whiteboard messages contain buttons and component interactions. Archiving removes action components to prevent edits.
- If a whiteboard's referenced message or channel is deleted, the whiteboard will be marked deleted and will not appear in lists.
