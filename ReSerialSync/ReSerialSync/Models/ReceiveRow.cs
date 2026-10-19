using CommunityToolkit.Mvvm.ComponentModel;

namespace ReSerialSync.Models;

public sealed class ReceiveRow : ObservableObject
{
    private string _text;

    public ReceiveRow(string text, string ending)
    {
        _text = text;
        Ending = ending;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public string Ending { get; set; }
}
