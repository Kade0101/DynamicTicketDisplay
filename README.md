# DynamicTicketDisplay

This repository contains the current Ticket Display proof of concept. The code is split into two applications that work together:

- `ServerAndClient/ticketdisplay-server/` - the host/operator application used to create and send ticket updates
- `ServerAndClient/ticketdisplay-client/` - the display application that listens for updates and renders them on screen

## How the server/display and host relate

The system is designed around two machines:

1. **Host machine**
   - Runs the operator-facing client solution
   - Holds the ticket visual information that staff enter or prepare
   - Sends ticket, prize, and clear commands to the display side

2. **Display machine**
   - Runs the display/server solution
   - Receives commands over TCP
   - Renders the current ticket visuals on the connected screen

## Current proof-of-concept setup

Right now the project is defaulted to a **single local machine** setup for testing and proof of concept work.

- The host/client app defaults to `127.0.0.1`
- Both applications can be run on the same machine
- This makes it easier to test message flow without needing separate hardware on the network

For a real deployment, the host machine would point to the IP address of the separate display machine instead of localhost.

## Expected message flow

1. An operator creates or updates ticket information on the host machine
2. The host/client app sends that payload to the display/server app over TCP
3. The display/server app receives the message and updates the on-screen ticket visuals

## Intended real-world deployment

The intended direction is:

- a dedicated display machine connected to the venue screen
- an external machine used by staff to control what appears on that display
- both devices connected on the same local network

The current localhost default is only there to support development, testing, and validation of the concept before moving to separate machines.
