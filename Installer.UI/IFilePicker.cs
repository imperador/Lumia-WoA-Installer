using System.Collections.Generic;
using Installer.Wpf.Core.Services;

namespace Installer.UI
{
    public interface IFilePicker
    {
        string InitialDirectory { get; set; }
        string SelectedFile { set; }
        List<FileTypeFilter> FileTypeFilter { get; }
        string PickFile();
    }
}