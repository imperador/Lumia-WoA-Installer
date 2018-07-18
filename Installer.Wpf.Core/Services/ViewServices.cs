using Cinch.Reloaded.Services.Interfaces;
using Installer.UI;
using MahApps.Metro.Controls.Dialogs;

namespace Installer.Wpf.Core.Services
{
    public class ViewServices
    {
        public ViewServices(IOpenFileService openFileService, IDialogCoordinator dialogCoordinator, IExtendedUIVisualizerService visualizerService, IFilePicker filePicker)
        {
            OpenFileService = openFileService;
            DialogCoordinator = dialogCoordinator;
            VisualizerService = visualizerService;
            FilePicker = filePicker;
        }

        public IOpenFileService OpenFileService { get; }
        public IDialogCoordinator DialogCoordinator { get; }
        public IExtendedUIVisualizerService VisualizerService { get; }
        public IFilePicker FilePicker { get; set; }
    }
}