# Honeypot

?> **Top-level invocation prefix:** `/dockadmin honeypot`

## Overview

The honeypot system helps server administrators remove spam accounts by automatically banning users who interact with configured trap areas. Honeypots can be triggered in two ways:

1. **Trap channel:** any non-bot user who sends a message in the configured channel is banned.
2. **Reaction trap:** any non-bot user who reacts to the configured message is banned.

The top-level **Enabled** setting is the master enforcement switch. When it is enabled, administrators can independently enable the message watcher, the reaction watcher, or both. When it is disabled, no honeypot bans are enforced.

When a user is banned, Dockhound posts a report embed to the configured report channel. If no report channel is set, it falls back to the server system channel or safety alerts channel when available.

!> **Warning** Honeypot actions are automatic bans. Place trap channels and trap messages where regular members will not accidentally interact with them.

## Recommended Setup

1. Create a private or clearly isolated trap channel.
2. Run `/dockadmin honeypot set-report-channel <channel>` so moderators can review honeypot activity.
3. Create a reaction trap with `/dockadmin honeypot create-honeypot`.
4. Optionally set a message trap channel with `/dockadmin honeypot set-channel <channel>`.
5. Confirm the active configuration with `/dockadmin honeypot status`.

## Commands

* [Status](DockAdmin/Honeypot?id=status): `/dockadmin honeypot status`
* [Enable](DockAdmin/Honeypot?id=enable): `/dockadmin honeypot enable`
* [Set Channel](DockAdmin/Honeypot?id=set-channel): `/dockadmin honeypot set-channel`
* [Set Report Channel](DockAdmin/Honeypot?id=set-report-channel): `/dockadmin honeypot set-report-channel`
* [Set Prune Days](DockAdmin/Honeypot?id=set-prune-days): `/dockadmin honeypot set-prune-days`
* [Set Reaction Message](DockAdmin/Honeypot?id=set-reaction-message): `/dockadmin honeypot set-reaction-message`
* [Create Honeypot](DockAdmin/Honeypot?id=create-honeypot): `/dockadmin honeypot create-honeypot`

---
### Status

**Description**

Show the current honeypot configuration for the server.

**Usage**

- `/dockadmin honeypot status`

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Shows whether enforcement is enabled.
- Shows whether the message watcher and reaction watcher are enabled.
- Shows the configured trap channel.
- Shows the configured reaction trap channel and message id.
- Shows the current message prune days setting.
- Shows the configured report channel, or the fallback behavior if one is not set.

---
### Enable

**Description**

Enable or disable honeypot enforcement.

**Usage**

- `/dockadmin honeypot enable <enabled> [messages_enabled] [reactions_enabled]`

**Parameters**

- `enabled`: `true` to enable enforcement, `false` to disable enforcement.
- `messages_enabled`: Optional. `true` to listen for trap-channel messages, `false` to ignore them.
- `reactions_enabled`: Optional. `true` to listen for reaction-trap reactions, `false` to ignore them.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Updates the server honeypot enabled state.
- If watcher parameters are omitted, their current settings are preserved.
- Enabling enforcement is rejected if both the message watcher and reaction watcher would be disabled.
- Disabling enforcement keeps the saved trap channel, reaction message, and watcher settings, but stops automatic bans until re-enabled.
- Valid active modes are messages only, reactions only, or both. Honeypot enforcement can also be globally disabled.

---
### Set Channel

**Description**

Set the trap channel. Any non-bot user who sends a message in this channel will be automatically banned while honeypot enforcement is enabled.

**Usage**

- `/dockadmin honeypot set-channel <channel>`

**Parameters**

- `channel`: The text channel to use as the message trap.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Saves the selected channel as the trap channel.
- Enables honeypot enforcement automatically.
- Does not change whether the message watcher is enabled. If the watcher is disabled, Dockhound warns that the trap will not ban until it is enabled.
- If a user triggers the trap, Dockhound attempts to ban them and posts a moderator report.

---
### Set Report Channel

**Description**

Set the channel where honeypot reports are posted.

**Usage**

- `/dockadmin honeypot set-report-channel <channel>`

**Parameters**

- `channel`: The text channel where ban, failure, and protected-user reports should be sent.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Saves the selected report channel.
- Honeypot reports include the user, account creation time, proof, and a review button when a ban succeeds.

---
### Set Prune Days

**Description**

Set how many days of a banned user's messages Discord should prune when the honeypot bans them.

**Usage**

- `/dockadmin honeypot set-prune-days <days>`

**Parameters**

- `days`: Number of days to prune. Must be between `0` and `7`.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Saves the prune setting used for future honeypot bans.
- Values outside `0` through `7` are rejected.

---
### Set Reaction Message

**Description**

Register an existing message as a reaction trap. Any non-bot user who reacts to that message will be automatically banned while honeypot enforcement is enabled.

**Usage**

- `/dockadmin honeypot set-reaction-message <channel> <message_id>`

**Parameters**

- `channel`: The text channel containing the trap message.
- `message_id`: The Discord message id for the trap message.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Saves the selected channel and message id as the reaction trap.
- Enables honeypot enforcement automatically.
- Does not change whether the reaction watcher is enabled. If the watcher is disabled, Dockhound warns that the trap will not ban until it is enabled.
- Rejects message ids that are not valid unsigned numbers.

---
### Create Honeypot

**Description**

Post a new honeypot embed in the current channel and register it as the reaction trap.

**Usage**

- `/dockadmin honeypot create-honeypot [content]`

**Parameters**

- `content`: Optional custom warning text for the honeypot embed.

**Permissions**

- Requires **Manage Server** permission.

**Behavior**

- Posts a honeypot embed in the current message channel.
- Saves the new message as the reaction trap.
- Enables honeypot enforcement automatically.
- Does not change whether the reaction watcher is enabled. If the watcher is disabled, Dockhound warns that the trap will not ban until it is enabled.
- The embed tracks **Bots Squashed** and **Saving Graces** counters.

## Moderator Review

Successful honeypot bans post a report with an **Unban** button. Moderators with **Ban Members** permission can use this button to reverse a false positive.

When a ban is reversed:

- The user is unbanned.
- The report embed is updated to show the reversal.
- The **Saving Graces** counter on the registered honeypot message is incremented when available.

## Protected Users

Dockhound will not automatically ban members who have privileged server permissions such as Administrator, Manage Server, Ban Members, Kick Members, Manage Roles, Manage Channels, Manage Messages, Moderate Members, Manage Nicknames, or Manage Webhooks.

If a protected user triggers the honeypot, Dockhound posts a skipped-ban report for moderator review instead.
