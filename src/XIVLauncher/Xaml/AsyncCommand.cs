using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XIVLauncher.Xaml
{
    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object parameter);
    }

    public abstract class AsyncCommandBase : IAsyncCommand
    {
        public abstract bool CanExecute(object parameter);
        public abstract Task ExecuteAsync(object parameter);
        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        protected void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public class AsyncCommand : AsyncCommandBase
    {
        private readonly Func<object, Task> _command;
        private readonly Func<bool> _canExecute;

        public AsyncCommand(Func<object, Task> command)
        {
            _command = command;
            _canExecute = () => true;
        }

        public AsyncCommand(Func<object, Task> command, Func<bool> canExecute)
        {
            _command = command;
            _canExecute = canExecute;
        }

        public override bool CanExecute(object parameter)
        {
            return _canExecute();
        }

        public override Task ExecuteAsync(object parameter)
        {
            return _command(parameter);
        }
    }
}