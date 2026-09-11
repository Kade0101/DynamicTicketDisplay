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

No workspace test suites are configured yet. Each workspace currently exits with a reminder message, so the root `npm test` command fails after running both workspace test commands.