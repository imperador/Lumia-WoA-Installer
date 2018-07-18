using System;
using ReactiveUI;
using Serilog.Events;

namespace Installer.ViewModels.Raspberry
{
    public class MainViewModel : ReactiveObject
    {
        private readonly IObservable<LogEvent> logEvents;


        public MainViewModel(IObservable<LogEvent> logEvents)
        {
            this.logEvents = logEvents;
        }

        public object ShowWarningCommand { get; }

        public object IsBusy { get; }

        public object Events { get; }

        public object ImportDriverPackageWrapper { get; }
    }
}