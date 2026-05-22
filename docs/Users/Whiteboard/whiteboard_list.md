# `/dock whiteboard list`

**Description**

List all whiteboards in the current channel.

**Usage**

- `/dock whiteboard list`

**Permissions**

- Any user may run this command inside a guild channel.

**Behavior**

- Queries the database for whiteboards in the guild/channel and validates that their referenced message still exists.
- Marks whiteboards as deleted (`IsDeleted`) if their message or channel is missing or inaccessible. Updated deletions are persisted.
- Returns a list of whiteboards with links to their messages if available. If the output is too long, it is returned as a text file attachment.
