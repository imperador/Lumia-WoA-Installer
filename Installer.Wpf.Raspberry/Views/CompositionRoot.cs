using System;
using Installer.ViewModels.Raspberry;
using Serilog.Events;

namespace Installer.Wpf.Raspberry.Views
{
    public class CompositionRoot
    {
        public static object GetMainViewModel(IObservable<LogEvent> logEvents)
        {
            return new MainViewModel(logEvents);
        }
    }
}