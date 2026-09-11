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

No workspace test suites are configured yet. Each workspace currently prints a reminder message and exits with a non-zero status when `npm test` is run from the repository root.