using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Viewer.Models;

public class FileItem : INotifyPropertyChanged
{
    private bool _isMatched;

    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long LastModified { get; set; }
    public string? Hash { get; set; }

    public bool IsMatched
    {
        get => _isMatched;
        set
        {
            if (_isMatched != value)
            {
                _isMatched = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
