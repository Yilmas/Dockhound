# Verification Settings

?>  **Top-level invocation prefix:** `/dockadmin verify`

## Overview

To faciliate the verification system, an info screen with Verification guidelines needs to be displayed, and depending on the state of the war - the restriction mode can be set.

## Info

**Description**

Post an informational verification embed in the current channel and save its channel/message id to guild settings. Useful to create or refresh an explanation and action area for verification flows.

**Usage**

- `/dockadmin verify info`

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Builds a verification info embed using the configured image and current restriction level.
- Posts the embed in the current channel and stores the posted channel and message id to the guild's restricted access settings.
- If called in a non-message channel context, returns the embed as an ephemeral response to the admin instead.

!> **Note** you can only have a single Info screen per server. 

## Restrict

**Description**

Set the verification access mode for the guild (Open / MembersOnly / Restricted). Also updates the saved info message if available.

**Usage**

- `/dockadmin verify restrict <setting>`

**Parameters**

- `setting`: AccessRestriction (Open | MembersOnly | Restricted).

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Updates the stored restricted access level.
- Attempts to update the persisted info message's embed to reflect the new setting. If the message or channel is missing or inaccessible, it will create a new info message in the command channel and save that reference.
