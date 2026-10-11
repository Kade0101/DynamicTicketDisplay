using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RaffleDisplayApplication;

public partial class InputWindow : Window
{
    public string TicketInfoString { get; private set; } = string.Empty;

    public TicketInfo? Ticket { get; private set; }

    public int SlotNumber { get; set; }

    public List<TicketInfo> TicketList { get; set; } = new();

    public InputWindow()
    {
        InitializeComponent();
    }

    private void SaveTicket(object? sender, RoutedEventArgs e)
    {
        HideValidation();

        if (string.IsNullOrWhiteSpace(LetterInput.Text) ||
            string.IsNullOrWhiteSpace(RaffleInput.Text) ||
            ColorInput.SelectedItem == null)
        {
            ShowValidation("Please fill in all fields.");
            return;
        }

        string letter = LetterInput.Text.Trim();

        if (letter.Length != 1 || !char.IsLetter(letter[0]))
        {
            ShowValidation("Please enter a single letter.");
            LetterInput.Focus();
            return;
        }

        if (!int.TryParse(RaffleInput.Text.Trim(), out int number))
        {
            ShowValidation("Please enter a valid number.");
            RaffleInput.Focus();
            return;
        }

        if (number < 0 || number > 100)
        {
            ShowValidation("Number must be between 0 and 100.");
            RaffleInput.Focus();
            return;
        }

        if (ColorInput.SelectedItem is not ComboBoxItem colorItem)
        {
            ShowValidation("Please choose a colour.");
            return;
        }

        string? color = colorItem.Content?.ToString();

        if (string.IsNullOrWhiteSpace(color))
        {
            ShowValidation("Please choose a colour.");
            return;
        }

        string numberString =
            number < 10 ? $"0{number}" : number.ToString();

        var ticket = new TicketInfo
        {
            Letter = letter.ToUpperInvariant(),
            Number = numberString,
            Color = color,
            SlotNumber = SlotNumber
        };

        if (TicketList.Exists(
                existing => existing.SlotNumber == ticket.SlotNumber))
        {
            ShowValidation(
                $"Slot {ticket.SlotNumber} already has a ticket.");
            return;
        }

        TicketList.Add(ticket);
        Ticket = ticket;

        TicketInfoString =
            $"{ticket.Letter} {ticket.Number} {ticket.Color}";

        Debug.WriteLine(
            $"Ticket Info: {TicketInfoString}");
        Close(true);
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.IsVisible = true;
    }

    private void HideValidation()
    {
        ValidationMessage.Text = string.Empty;
        ValidationMessage.IsVisible = false;
    }

    public string GetTicketInfo()
    {
        return TicketInfoString;
    }

    public TicketInfo? GetTicket()
    {
        return Ticket;
    }
}
