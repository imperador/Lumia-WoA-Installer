using System.Threading.Tasks;
using Installer.Core.FileSystem;
using Serilog;

namespace Installer.Core.Raspberry
{
    public class RaspberryPi : Device
    {
        public RaspberryPi(Disk disk) : base(disk)
        {
        }

        public override async Task RemoveExistingWindowsPartitions()
        {
            await Task.CompletedTask;
        }

        public override async Task<Volume> GetBootVolume()
        {
            return boolVolume ?? (boolVolume = await GetVolume("EFIESP"));
        }
    }
}