using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Solver
{
    public class DisplayableSolutionWord : INotifyPropertyChanged
    {
        public string Word => FullPathData?.Word ?? string.Empty;
        public WordPath FullPathData { get; }

        private bool _isUserIncluded;
        public bool IsUserIncluded
        {
            get => _isUserIncluded;
            set => SetField(ref _isUserIncluded, value);
        }

        private bool _isUserExcluded;
        public bool IsUserExcluded
        {
            get => _isUserExcluded;
            set => SetField(ref _isUserExcluded, value);
        }

        public DisplayableSolutionWord(WordPath pathData)
        {
            FullPathData = pathData;
            // Initial state (IsUserIncluded, IsUserExcluded) is set by MainWindow logic
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}