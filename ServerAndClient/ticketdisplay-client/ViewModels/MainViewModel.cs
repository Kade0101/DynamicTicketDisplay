using CommunityToolkit.Mvvm.ComponentModel;

namespace ticketdisplay_client_modern.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private string _greeting = "Welcome to Avalonia!";

    public string Greeting
    {
        get => _greeting;
        set => SetProperty(ref _greeting, value);
    }
}
