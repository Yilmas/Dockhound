# Verification Overview

**Top-level invocation prefixes:** `/dock verify` (user interactions) and `/dockadmin verify` (administration)

## Overview

The verification system lets users request Recruit or Ally status via context commands or buttons exposed in an info message. A typical flow:

1. **Info message:** An admin posts an info embed using `/dockadmin verify info` describing verification and presenting action buttons.
2. **User request:** A user clicks a button or uses a context command such as *Request Recruit* or *Request Ally* to submit a verification request.
3. **Review:** The request is sent to a configured review channel where staff review the request and can approve or deny via bot actions.
4. **Outcome:** On approval, the user's verification record is updated; on denial the user is notified.

### Trusted Roles

- **Trusted Roles** are roles listed in guild settings that may grant auto-approval for certain requests. If a user has one of the Trusted Roles, the bot may auto-approve their request depending on server configuration.

**Admin note: Restriction mode**

- Administrators can change the verification workflow/mode using `/dockadmin verify restrict`. See the **DockAdmin** section for details on how modes affect available actions.


## Request Ally

**Command**

`Request Ally` (User context command)

**Description**

Used by members to request Ally status. Similar to Request Recruit but marks the request as an Ally request.

**Flow**

1. The user invokes Request Ally or clicks the appropriate button on the verification info message.
2. A verification record is created and sent to the review channel for moderator action.
3. Moderators approve or deny the request as usual.

**Trusted Roles**

- Trusted roles may lead to auto-approval depending on configuration.

**Notes**

- Ally requests can be used to record ally relationships or grant limited privileges depending on your server setup.

## Request Recruit

**Command**

`Request Recruit` (User context command)

**Description**

Used by members to request Recruit status. This can be invoked from a user's profile (User context) or from a message (Message context) depending on how the command is registered.

**Flow**

1. The user invokes Request Recruit or clicks the appropriate button on the verification info message.
2. A verification record is created and the request is posted to the configured review channel for moderators.
3. Moderators review the submission and can approve or deny. On approval the user's record is finalized; on denial the user is informed and the record is marked accordingly.

**Trusted Roles**

- Members with roles listed in `TrustedRoles` may receive auto-approval depending on your guild's configuration.

**Notes**

- The exact fields collected in the request (image, optional Steam ID, etc.) depend on guild settings.
