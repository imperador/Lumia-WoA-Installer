using System;
using Installer.Core.FullFx;
using Installer.Core.Services;
using Installer.UI;
using Installer.ViewModels.Raspberry;
using Installer.Wpf.Core.Services;
using MahApps.Metro.Controls.Dialogs;
using Serilog.Events;

namespace Installer.Wpf.Raspberry.Views
{
    public static class CompositionRoot
    {
        public static object GetMainViewModel(IObservable<LogEvent> logEvents)
        {
            return new MainViewModel(logEvents, new DiskService(new LowLevelApi()), new UIServices(new FilePicker(), new ViewService(), new DialogService(DialogCoordinator.Instance)));
        }
    }
}