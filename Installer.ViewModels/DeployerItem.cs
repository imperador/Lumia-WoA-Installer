using Installer.Core;
using Installer.Core.Lumia;

namespace Installer.ViewModels.Lumia
{
    public class DeployerItem
    {
        public PhoneModel Model { get; }
        public IDeployer<Phone> Deployer { get; }

        public DeployerItem(PhoneModel model, IDeployer<Phone> deployer)
        {
            Model = model;
            Deployer = deployer;
        }
    }
}