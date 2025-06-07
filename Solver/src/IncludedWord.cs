using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Solver;

/// <summary>
/// Represents a word in the included or excluded lists, with status information.
/// </summary>
public class IncludedWord : INotifyPropertyChanged
{
    private readonly string _word;
    private bool _hasPath;

    public string Word => _word;
    public event PropertyChangedEventHandler PropertyChanged;

    public bool HasPath
    {
        get => _hasPath;
        set
        {
            if (SetField(ref _hasPath, value))
            {
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    // For data binding in XAML to display validation/warning message
    public string StatusMessage => HasPath ? string.Empty : "(no path found on board)";

    public IncludedWord(string word, bool hasPath = true)
    {
        _word = word;
        _hasPath = hasPath;
    }

    public override string ToString() => _word;

    // Override Equals and GetHashCode for correct collection operations
    public override bool Equals(object obj)
    {
        if (obj is IncludedWord other)
            return string.Equals(Word, other.Word, StringComparison.OrdinalIgnoreCase);

        if (obj is string str)
            return string.Equals(Word, str, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Word);
    }


    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}