namespace Installer.ViewModels.Raspberry
{
    public interface ISettingsService
    {
        string DriverPackFolder { get; set; }
        string WimFolder { get; set; }
    }
}