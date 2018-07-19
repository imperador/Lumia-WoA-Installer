using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Installer.Core;
using Installer.Core.Exceptions;
using Installer.Core.FileSystem;
using Installer.Core.Raspberry;
using Installer.Core.Services;
using Installer.Core.Services.Wim;
using Installer.UI;
using Installer.ViewModels.Core;
using ReactiveUI;
using Serilog;
using Serilog.Events;

namespace Installer.ViewModels.Raspberry
{
    public class MainViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<RenderedLogEvent> statusHelper;
        private readonly ReadOnlyObservableCollection<RenderedLogEvent> logEvents;
        private readonly ObservableAsPropertyHelper<bool> hasWimHelper;
        private readonly IDeployer<RaspberryPi> deployer;
        private readonly UIServices uiServices;
        private readonly ISettingsService settingsService;
        private readonly Func<Task<RaspberryPi>> getRaspberryPiFunc;
        private readonly ObservableAsPropertyHelper<IEnumerable<DiskViewModel>> disksHelper;
        private ObservableAsPropertyHelper<WimMetadataViewModel> pickWimFileObs;
        public WimMetadataViewModel WimMetadata => pickWimFileObs.Value;
        private readonly ISubject<double> progressSubject = new BehaviorSubject<double>(double.NaN);
        private readonly IDisposable logLoader;

        public MainViewModel(IObservable<LogEvent> logEvents, IDeployer<RaspberryPi> deployer, DiskService diskService,
            UIServices uiServices, ISettingsService settingsService)
        {
            this.deployer = deployer;
            this.uiServices = uiServices;
            this.settingsService = settingsService;
            RefreshDisksCommmandWrapper = new CommandWrapper<Unit, ICollection<Disk>>(this, ReactiveCommand.CreateFromTask(diskService.GetDisks), uiServices.DialogService);
            disksHelper = RefreshDisksCommmandWrapper.Command
                .Select(x => x
                    .Where(y => !y.IsBoot && !y.IsSystem && !y.IsOffline)
                    .Select(disk => new DiskViewModel(disk)))
                .ToProperty(this, x => x.Disks);

            ShowWarningCommand = ReactiveCommand.CreateFromTask(() =>
                uiServices.DialogService.ShowAlert(this, Resources.TermsOfUseTitle,
                    Resources.WarningNotice));

            SetupPickWimCommand();

            var whenAnyValue = this.WhenAnyValue(x => x.SelectedDisk, (DiskViewModel disk) => disk != null);

            var canDeploy = this.WhenAnyObservable(x => x.WimMetadata.SelectedImageObs)
                .Select(metadata => metadata != null)
                .CombineLatest(whenAnyValue, (isWimSelected, isDiskSelected) => isDiskSelected && isWimSelected);

            FullInstallWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(DeployUefiAndWindows, canDeploy), uiServices.DialogService);

            var isBusyObs = FullInstallWrapper.Command.IsExecuting;

            isBusyHelper = isBusyObs.ToProperty(this, model => model.IsBusy);

            progressHelper = progressSubject
                .Where(d => !double.IsNaN(d))
                .ObserveOn(SynchronizationContext.Current)
                .ToProperty(this, model => model.Progress);

            isProgressVisibleHelper = progressSubject
                .Select(d => !double.IsNaN(d))
                .ToProperty(this, x => x.IsProgressVisible);

            logLoader = logEvents
                .Where(x => x.Level == LogEventLevel.Information)
                .ToObservableChangeSet()
                .Transform(x => new RenderedLogEvent
                {
                    Message = x.RenderMessage(),
                    Level = x.Level
                })
                .Bind(out this.logEvents)
                .DisposeMany()
                .Subscribe();

            statusHelper = logEvents
                .Where(x => x.Level == LogEventLevel.Information)
                .Select(x => new RenderedLogEvent
                {
                    Message = x.RenderMessage(),
                    Level = x.Level
                })
                .ToProperty(this, x => x.Status);

            hasWimHelper = this.WhenAnyValue(model => model.WimMetadata, (WimMetadataViewModel x) => x != null)
                .ToProperty(this, x => x.HasWim);
        }

        public bool HasWim => hasWimHelper.Value;

        private async Task DeployUefiAndWindows()
        {
            var installOptions = new InstallOptions
            {
                ImagePath = WimMetadata.Path,
                ImageIndex = WimMetadata.SelectedDiskImage.Index,
            };

            var raspberryPi = new RaspberryPi(SelectedDisk.Disk);
            await deployer.DeployCoreAndWindows(installOptions, raspberryPi, progressSubject);
            await uiServices.DialogService.ShowAlert(this, Resources.Finished,
                Resources.WindowsDeployedSuccessfully);
        }

        public RenderedLogEvent Status => statusHelper.Value;

        public CommandWrapper<Unit, Unit> FullInstallWrapper { get; set; }

        private void SetupPickWimCommand()
        {
            PickWimFileCommand = ReactiveCommand.CreateFromObservable(() => PickWimFileObs);
            pickWimFileObs = PickWimFileCommand.ToProperty(this, x => x.WimMetadata);
            PickWimFileCommand.ThrownExceptions.Subscribe(e =>
            {
                Log.Error(e, "WIM file error");
                uiServices.DialogService.ShowAlert(this, "Invalid WIM file", e.Message);
            });
        }

        private IObservable<WimMetadataViewModel> PickWimFileObs
        {
            get
            {
                var value = uiServices.FilePicker.Pick(new List<(string, IEnumerable<string>)> { ("WIM files", new[] { "install.wim" }) },
                    () => settingsService.WimFolder, x => settingsService.WimFolder = x);

                return Observable.Return(value).Where(x => x != null)
                    .Select(LoadWimMetadata);
            }
        }

        private static WimMetadataViewModel LoadWimMetadata(string path)
        {
            Log.Verbose("Trying to load WIM metadata file at '{ImagePath}'", path);

            using (var file = File.OpenRead(path))
            {
                var imageReader = new WindowsImageMetadataReader();
                var windowsImageInfo = imageReader.Load(file);
                if (windowsImageInfo.Images.All(x => x.Architecture != Architecture.Arm64))
                {
                    throw new InvalidWimFileException(Resources.WimFileNoValidArchitecture);
                }

                var vm = new WimMetadataViewModel(windowsImageInfo, path);

                Log.Verbose("WIM metadata file at '{ImagePath}' retrieved correctly", path);

                return vm;
            }
        }

        public ReactiveCommand<Unit, WimMetadataViewModel> PickWimFileCommand { get; set; }

        public CommandWrapper<Unit, ICollection<Disk>> RefreshDisksCommmandWrapper { get; set; }

        public object ShowWarningCommand { get; }

        public bool IsBusy => isBusyHelper.Value;
        private readonly ObservableAsPropertyHelper<bool> isBusyHelper;
        private DiskViewModel selectedDisk;

        public ReadOnlyObservableCollection<RenderedLogEvent> Events => logEvents;

        public object ImportDriverPackageWrapper { get; }

        public IEnumerable<DiskViewModel> Disks => disksHelper.Value;

        public DiskViewModel SelectedDisk
        {
            get => selectedDisk;
            set => this.RaiseAndSetIfChanged(ref selectedDisk, value);
        }

        public double Progress => progressHelper.Value;

        private readonly ObservableAsPropertyHelper<double> progressHelper;

        public bool IsProgressVisible => isProgressVisibleHelper.Value;

        private readonly ObservableAsPropertyHelper<bool> isProgressVisibleHelper;
    }
}