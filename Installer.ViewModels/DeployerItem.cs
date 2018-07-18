using Installer.Core;
using Intaller.Wpf.ViewModels;

namespace Installer.ViewModels
{
    public class DeployerItem
    {
        public PhoneModel Model { get; }
        public IDeployer Deployer { get; }

        public DeployerItem(PhoneModel model, IDeployer deployer)
        {
            Model = model;
            Deployer = deployer;
        }
    }
}