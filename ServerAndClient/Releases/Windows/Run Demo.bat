@echo off
title Dynamic Ticket Display

echo Starting Ticket Display Server...
start "Ticket Server" "%~dp0Server\TicketAppV1.2.9.exe"

timeout /t 2 /nobreak >nul

echo Starting Avalonia Client...
start "Ticket Client" "%~dp0Client\ticketdisplay-client-modern.exe"

exit