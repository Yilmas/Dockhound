# `/dockadmin verify info`

**Description**

Post an informational verification embed in the current channel and save its channel/message id to guild settings. Useful to create or refresh an explanation and action area for verification flows.

**Usage**

- `/dockadmin verify info`

**Permissions**

- Requires **Administrator** permission.

**Behavior**

- Builds a verification info embed using the configured image and current restriction level.
- Posts the embed in the current channel and stores the posted channel and message id to the guild's restricted access settings.
- If called in a non-message channel context, returns the embed as an ephemeral response to the admin instead.
