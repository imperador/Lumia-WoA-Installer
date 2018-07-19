using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Installer.Core.FileSystem;
using Installer.Core.Services;
using Installer.UI;
using Installer.ViewModels.Core;
using ReactiveUI;
using Serilog.Events;

namespace Installer.ViewModels.Raspberry
{
    public class MainViewModel : ReactiveObject
    {
        private readonly IObservable<LogEvent> logEvents;
        private readonly ObservableAsPropertyHelper<IEnumerable<DiskViewModel>> disksHelper;


        public MainViewModel(IObservable<LogEvent> logEvents, DiskService diskService, UIServices uiServices)
        {
            this.logEvents = logEvents;
            RefreshDisksCommmandWrapper = new CommandWrapper<Unit, ICollection<Disk>>(this, ReactiveCommand.CreateFromTask(diskService.GetDisks), uiServices.DialogService);
            disksHelper = RefreshDisksCommmandWrapper.Command
                .Select(x => x
                    .Where(y => !y.IsBoot && !y.IsSystem && !y.IsOffline)
                    .Select(disk => new DiskViewModel(disk)))
                .ToProperty(this, x => x.Disks);
        }

        public CommandWrapper<Unit, ICollection<Disk>> RefreshDisksCommmandWrapper { get; set; }

        public object ShowWarningCommand { get; }

        public object IsBusy { get; }

        public object Events { get; }

        public object ImportDriverPackageWrapper { get; }

        public IEnumerable<DiskViewModel> Disks => disksHelper.Value;
    }
}