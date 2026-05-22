# `/dockadmin settings logs-per-message`

**Description**

Show all log events for a given message id over a specified timespan in days. Groups results by ISO week and displays counts per week.

**Usage**

- `/dockadmin settings logs-per-message <message_id> <days_span>`

**Parameters**

- `message_id`: Discord message ID to look up.
- `days_span`: Days into the past to include (positive number). The command internally negates it to compute the cutoff.

**Permissions**

- Requires **View Audit Log** and **Manage Messages** permissions.

**Behavior**

- Validates the message id format and computes a cutoff date.
- Queries the LogEvents table for matches on MessageId and Updated >= cutoff.
- Groups events by ISO year+week and renders weekly lines, including weeks with zero updates.
- Returns an embed showing total events, weeks covered, and a code block with weekly updates.

**Notes**

- Uses ISO week handling to present consistent weekly buckets starting on Monday.
