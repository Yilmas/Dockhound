# Request Recruit Context Command / Flow

**Command**

`Request Recruit` (Message/User context command)

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
