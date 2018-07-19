using System;
using System.Threading.Tasks;
using Installer.Core.Services;
using Serilog;

namespace Installer.Core.Raspberry
{
    public class RaspberryPiDeployer : IDeployer<RaspberryPi>
    {
        private readonly IImageFlasher flasher;

        public RaspberryPiDeployer(IImageFlasher flasher)
        {
            this.flasher = flasher;
        }

        public async Task DeployCoreAndWindows(InstallOptions options, RaspberryPi device, IObserver<double> progressObserver = null)
        {
            Log.Information("Flashing GPT image...");
            await flasher.Flash(device.Disk, @"Files\Core\gpt.zip", progressObserver);
            Log.Information("GPT image flashed");
        }

        public Task DeployWindows(InstallOptions options, RaspberryPi device, IObserver<double> progressObserver = null)
        {
            throw new NotImplementedException();
        }

        public Task InjectPostOobeDrivers(RaspberryPi device)
        {
            throw new NotImplementedException();
        }
    }
}