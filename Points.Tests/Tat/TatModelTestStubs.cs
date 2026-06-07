using System.Windows.Input;

namespace CommunityToolkit.Maui.Views
{
}

namespace Points.Converters
{
}

namespace Points.Models
{
    public sealed class Command : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public Command(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }

        public void ChangeCanExecute()
        {
        }
    }
}
