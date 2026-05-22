# Verification — Overview & Flow

**Top-level invocation prefixes:** `/dock verify` (user interactions) and `/dockadmin verify` (administration)

## Overview

The verification system lets users request Recruit or Ally status via context commands or buttons exposed in an info message. A typical flow:

1. **Info message:** An admin posts an info embed using `/dockadmin verify info` describing verification and presenting action buttons.
2. **User request:** A user clicks a button or uses a context command such as *Request Recruit* or *Request Ally* to submit a verification request.
3. **Review:** The request is sent to a configured review channel where staff review the request and can approve or deny via bot actions.
4. **Outcome:** On approval, the user's verification record is updated; on denial the user is notified.

## Trusted Roles

- **Trusted Roles** are roles listed in guild settings that may grant auto-approval for certain requests. If a user has one of the Trusted Roles, the bot may auto-approve their request depending on server configuration.

## Admin note: Restriction mode

- Administrators can change the verification workflow/mode using `/dockadmin verify restrict`. See the **DockAdmin** section for details on how modes affect available actions.

## Subpages

- **Request Recruit:** `docs/Users/Verification/request_recruit.md`
- **Request Ally:** `docs/Users/Verification/request_ally.md`

