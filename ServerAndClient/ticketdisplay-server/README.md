<<<<<<< HEAD
# DynamicTicketDisplay

This repository is organized as a monorepo with separate workspaces for the TicketDisplay server and client.

## Workspaces

- `apps/ticketdisplay-server`
- `apps/ticketdisplay-client`

## Tests

From the repository root:

```bash
npm test
```

No workspace test suites are configured yet. Each workspace currently prints a reminder message when `npm test` is run from the repository root.
=======
This program is a work in progress.

Purpose is to be hopefully implemented in a real-life environment where getting information to customers in a venue is difficult (Sound restrictions, loud ambient volume or large crowds). This program requires at least a Raspberry Pi 4 with linux arm 64, HDMI connection to a screen and a Windows Operating System on the same network.

Ticket Display_Server is to be built on the Pi (or other system with linux installed and wifi capability). Ticket Display_Client ran on the windows system for wireless control.  

At this stage, it can display ticket number and colour. future idea is to also have an option of displaying custom text for important information.
>>>>>>> temp-server/master
