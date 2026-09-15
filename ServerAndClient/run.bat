@echo off
echo Extending network download timeout to prevent NuGet drops...
set DOTNET_HTTP_TIMEOUT=00:10:00

echo Firing up the .NET Backend Server...
cd ticketdisplay-server
start "Ticket Server" dotnet run
cd ..

echo Firing up the Modern Avalonia Client App...
cd ticketdisplay-client-modern
start "Ticket Client" dotnet run
cd ..

echo Both systems are compiling and booting side-by-side!
