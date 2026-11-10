using System;
using System.IO;
using com.clusterrr.hakchi_gui;
using Xunit;

namespace hakchi_gui.Tests.Forms
{
    public class WaitingFelFormTests
    {
        [Fact]
        public void PnpUtilPathUsesSysnativeFor32BitProcessOn64BitWindows()
        {
            Assert.Equal(@"C:\Windows\Sysnative\pnputil.exe", WaitingFelForm.PnpUtilPath(false, true, @"C:\Windows"));
            Assert.Equal(@"C:\Windows\System32\pnputil.exe", WaitingFelForm.PnpUtilPath(true, true, @"C:\Windows"));
            Assert.Equal(@"C:\Windows\System32\pnputil.exe", WaitingFelForm.PnpUtilPath(false, false, @"C:\Windows"));
        }

        [Fact]
        public void PnpUtilResolvedForThisProcessExists()
        {
            // Les tests tournent en x86, comme hakchi : c'est exactement le cas qui plantait.
            var path = WaitingFelForm.PnpUtilPath(Environment.Is64BitProcess, Environment.Is64BitOperatingSystem,
                Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            Assert.True(File.Exists(path), path);
        }

        [Fact]
        public void DriverInstalledDoesNotThrow()
        {
            var ex = Record.Exception(() => WaitingFelForm.DriverInstalled());
            Assert.Null(ex);
        }
    }
}
