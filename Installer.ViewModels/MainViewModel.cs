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
using Installer.Core.Lumia;
using Installer.Core.Services.Wim;
using Installer.UI;
using Installer.ViewModels.Core;
using ReactiveUI;
using Serilog;
using Serilog.Events;

namespace Installer.ViewModels.Lumia
{
    public class MainViewModel : ReactiveObject, IDisposable
    {
        private readonly Func<Task<Phone>> getPhoneFunc;
        private readonly ObservableAsPropertyHelper<bool> isBusyHelper;
        private readonly ReadOnlyObservableCollection<RenderedLogEvent> logEvents;
        private readonly ObservableAsPropertyHelper<double> progressHelper;
        private readonly ISubject<double> progressSubject = new BehaviorSubject<double>(double.NaN);
        private readonly ObservableAsPropertyHelper<RenderedLogEvent> statusHelper;
        private readonly IDisposable logLoader;
        private readonly ICollection<DriverPackageImporterItem> driverPackageImporterItems;
        private readonly UIServices uiServices;
        private readonly ISettingsService settingService;
        private readonly ObservableAsPropertyHelper<bool> isProgressVisibleHelper;
        private readonly ObservableAsPropertyHelper<bool> hasWimHelper;
        private ObservableAsPropertyHelper<WimMetadataViewModel> pickWimFileObs;
        private DeployerItem selectedDeployerItem;

        public MainViewModel(IObservable<LogEvent> logEvents, ICollection<DeployerItem> deployersItems, ICollection<DriverPackageImporterItem> driverPackageImporterItems, UIServices uiServices, ISettingsService settingService, Func<Task<Phone>> getPhoneFunc)
        {
            DualBootViewModel = new DualBootViewModel(uiServices.DialogService, getPhoneFunc);

            DeployersItems = deployersItems;

            this.driverPackageImporterItems = driverPackageImporterItems;
            this.uiServices = uiServices;
            this.settingService = settingService;
            this.getPhoneFunc = getPhoneFunc;

            ShowWarningCommand = ReactiveCommand.CreateFromTask(() =>
                uiServices.DialogService.ShowAlert(this, Resources.TermsOfUseTitle,
                    Resources.WarningNotice));

            SetupPickWimCommand();

            var isDeployerSelected =
                this.WhenAnyValue(model => model.SelectedDeployerItem, (DeployerItem x) => x != null);
            var isSelectedWim = this.WhenAnyObservable(x => x.WimMetadata.SelectedImageObs)
                .Select(metadata => metadata != null);

            var canDeploy =
                isSelectedWim.CombineLatest(isDeployerSelected, (hasWim, hasDeployer) => hasWim && hasDeployer);

            FullInstallWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(DeployUefiAndWindows, canDeploy), uiServices.DialogService);
            WindowsInstallWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(DeployWindows, canDeploy), uiServices.DialogService);
            InjectDriversWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(InjectPostOobeDrivers, isDeployerSelected),
                uiServices.DialogService);

            ImportDriverPackageWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(ImportDriverPackage), uiServices.DialogService);

            var isBusyObs = Observable.Merge(FullInstallWrapper.Command.IsExecuting,
                WindowsInstallWrapper.Command.IsExecuting,
                InjectDriversWrapper.Command.IsExecuting,
                ImportDriverPackageWrapper.Command.IsExecuting);

            var dualBootIsBusyObs = DualBootViewModel.IsBusyObs;

            isBusyHelper = Observable.Merge(isBusyObs, dualBootIsBusyObs)
                .ToProperty(this, model => model.IsBusy);

            progressHelper = progressSubject
                .Where(d => !double.IsNaN(d))
                .ObserveOn(SynchronizationContext.Current)
                .ToProperty(this, model => model.Progress);

            isProgressVisibleHelper = progressSubject
                .Select(d => !double.IsNaN(d))
                .ToProperty(this, x => x.IsProgressVisible);

            statusHelper = logEvents
                .Where(x => x.Level == LogEventLevel.Information)
                .Select(x => new RenderedLogEvent
                {
                    Message = x.RenderMessage(),
                    Level = x.Level
                })
                .ToProperty(this, x => x.Status);

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

            hasWimHelper = this.WhenAnyValue(model => model.WimMetadata, (WimMetadataViewModel x) => x != null)
                .ToProperty(this, x => x.HasWim);
        }

        public IEnumerable<DeployerItem> DeployersItems { get; }

        public DeployerItem SelectedDeployerItem
        {
            get => selectedDeployerItem;
            set => this.RaiseAndSetIfChanged(ref selectedDeployerItem, value);
        }

        private IDeployer<Phone> SelectedDeployer => SelectedDeployerItem.Deployer;

        public bool HasWim => hasWimHelper.Value;

        private async Task ImportDriverPackage()
        {
            var extensions = driverPackageImporterItems.Select(x => $"*.{x.Extension}");

            var fileName = uiServices.FilePicker.Pick(new List<(string, IEnumerable<string>)> { ("Driver package", extensions) }, () => settingService.DriverPackFolder, fn => settingService.DriverPackFolder = fn);

            if (fileName == null)
            {
                return;
            }

            var item = GetImporterItemForFile(fileName);
            var importer = item.DriverPackageImporter;

            var message = await importer.GetReadmeText(fileName);
            if (!string.IsNullOrEmpty(message))
            {
                uiServices.ViewService.Show("TextViewer", new MessageViewModel("Changelog", message));
            }

            await importer.ImportDriverPackage(fileName, "", progressSubject);
            await uiServices.DialogService.ShowAlert(this, "Done", "Driver Package imported");
            Log.Information("Driver Package imported");
        }

        private DriverPackageImporterItem GetImporterItemForFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);

            var importerItem = driverPackageImporterItems.First(item =>
                string.Equals(extension, "." + item.Extension, StringComparison.InvariantCultureIgnoreCase));
            return importerItem;
        }

        public CommandWrapper<Unit, Unit> ImportDriverPackageWrapper { get; }

        public CommandWrapper<Unit, Unit> InjectDriversWrapper { get; }

        public ReactiveCommand<Unit, Unit> ShowWarningCommand { get; set; }

        public bool IsProgressVisible => isProgressVisibleHelper.Value;

        public CommandWrapper<Unit, Unit> WindowsInstallWrapper { get; set; }

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
                var value = uiServices.FilePicker.Pick(new List<(string, IEnumerable<string>)> {("WIM files", new[] {"install.wim"})},
                    () => settingService.WimFolder, x => settingService.WimFolder = x);

                return Observable.Return(value).Where(x => x != null)
                    .Select(LoadWimMetadata);
            }
        }

        public WimMetadataViewModel WimMetadata => pickWimFileObs.Value;

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

        private async Task DeployUefiAndWindows()
        {
            var installOptions = new InstallOptions
            {
                ImagePath = WimMetadata.Path,
                ImageIndex = WimMetadata.SelectedDiskImage.Index,
            };

            await SelectedDeployer.DeployCoreAndWindows(installOptions, await GetPhone(), progressSubject);
            await uiServices.DialogService.ShowAlert(this, Resources.Finished,
                Resources.WindowsDeployedSuccessfully);
        }

        private Task<Phone> GetPhone()
        {
            return getPhoneFunc();
        }

        private async Task DeployWindows()
        {
            var installOptions = new InstallOptions
            {
                ImagePath = WimMetadata.Path,
                ImageIndex = WimMetadata.SelectedDiskImage.Index,
            };

            await SelectedDeployer.DeployWindows(installOptions, await GetPhone(), progressSubject);
            await uiServices.DialogService.ShowAlert(this, Resources.Finished,
                Resources.WindowsDeployedSuccessfully);
        }

        private async Task InjectPostOobeDrivers()
        {
            try
            {
                await SelectedDeployer.InjectPostOobeDrivers(await GetPhone());
            }
            catch (DirectoryNotFoundException e)
            {
                throw new InvalidOperationException(Resources.NoPostOobeDrivers, e);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(Resources.CannotInjectPostOobe, e);
            }

            await uiServices.DialogService.ShowAlert(this, Resources.Finished,
                Resources.DriversInjectedSucessfully);
        }

        public ReadOnlyObservableCollection<RenderedLogEvent> Events => logEvents;

        public RenderedLogEvent Status => statusHelper.Value;

        public bool IsBusy => isBusyHelper.Value;

        public CommandWrapper<Unit, Unit> FullInstallWrapper { get; set; }
        public double Progress => progressHelper.Value;

        public DualBootViewModel DualBootViewModel { get; }

        public void Dispose()
        {
            isBusyHelper?.Dispose();
            progressHelper?.Dispose();
            statusHelper?.Dispose();
            logLoader?.Dispose();
            isProgressVisibleHelper?.Dispose();
            hasWimHelper?.Dispose();
            ShowWarningCommand?.Dispose();
            PickWimFileCommand?.Dispose();
        }
    }

    public interface ISettingsService
    {
        string DriverPackFolder { get; set; }
        string WimFolder { get; set; }
    }
}