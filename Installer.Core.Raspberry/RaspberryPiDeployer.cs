using System;
using System.Threading.Tasks;

namespace Installer.Core.Raspberry
{
    public class RaspberryPiDeployer : IDeployer<RaspberryPi>
    {
        public Task DeployCoreAndWindows(InstallOptions options, RaspberryPi device, IObserver<double> progressObserver = null)
        {
            throw new NotImplementedException();
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