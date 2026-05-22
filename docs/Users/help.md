# `/dock help`

**Description**

Displays a list of commands the invoking user can use in the current context (guild or DM). The command inspects registered slash and context commands, shows required permissions, and organizes commands by module and nested slash group.

**Usage**

- `/dock help`

**Permissions**

None required; the list shown is filtered to only commands the user has permission to run in the current context.

**Notes**

- The output is an embedded message with a concise description for each command, including parameter summaries.
- When used in a DM, only commands that are enabled in DMs will be displayed.
