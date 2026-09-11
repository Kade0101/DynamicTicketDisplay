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

No workspace test suites are configured yet. Each workspace currently prints a reminder message, and the root `npm test` command exits with a non-zero status after running both workspace placeholders.