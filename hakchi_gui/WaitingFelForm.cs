using com.clusterrr.hakchi_gui.Properties;
using FelLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui
{
    public partial class WaitingFelForm : com.clusterrr.hakchi_gui.Theme.B1Form
    {
        bool deviceFound = false;

        /// <summary>
        /// pnputil.exe n'existe qu'en 64 bits (System32). hakchi tourne en 32 bits (AnyCPU, Prefer32Bit) :
        /// la redirection WOW64 envoie "pnputil.exe" vers SysWOW64, ou il est absent -> Win32Exception
        /// "fichier introuvable". On passe par Sysnative quand le processus 32 bits tourne sur un Windows 64 bits.
        /// </summary>
        internal static string PnpUtilPath(bool is64BitProcess, bool is64BitOs, string windowsDir)
        {
            if (!is64BitProcess && is64BitOs)
                return Path.Combine(windowsDir, "Sysnative", "pnputil.exe");
            return Path.Combine(windowsDir, "System32", "pnputil.exe");
        }

        public static bool DriverInstalled()
        {
            if (!Shared.isWindows)
                return true;

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = PnpUtilPath(Environment.Is64BitProcess, Environment.Is64BitOperatingSystem,
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
                    Arguments = "-e",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            return proc.StandardOutput.ReadToEnd().Contains("USB\\VID_1F3A&PID_EFE8");
        }

        public static int InstallDriver()
        {
            try
            {
                int exitCode = 0;
                TempHelpers.doWithTempFolder((string temp) =>
                {
                    var fileName = Path.Combine(Path.Combine(Program.BaseDirectoryInternal, "driver"), "classic_driver.exe");
                    var process = new Process();
                    process.StartInfo.FileName = fileName;
                    process.StartInfo.WorkingDirectory = temp;
                    process.StartInfo.Verb = "runas";
                    process.Start();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                });
                return exitCode;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                return -1;
            }
        }

        public WaitingFelForm()
        {
            InitializeComponent();

            bool driverOk;
            try
            {
                driverOk = DriverInstalled() || InstallDriver() == 0;
            }
            catch (Exception ex)
            {
                // Detection impossible : on laisse le panneau pilote visible (installation manuelle)
                // au lieu de faire planter l'attente de la console.
                Trace.WriteLine(ex.ToString());
                driverOk = false;
            }
            if (driverOk)
            {
                tableLayoutPanelDriver.Visible = false;
            }

            timer.Enabled = true;
        }

        public static bool WaitForDevice(IWin32Window owner)
        {
            if (Fel.DeviceExists())
                return true;
            var form = new WaitingFelForm();
            form.ShowDialog(owner);
            return form.DialogResult == DialogResult.OK;
        }

        private static bool AttemptUpdateHandshake()
        {
            var result = false;
            using (Fel fel = new Fel())
            {
                var probeSuccess = false;

                try
                {
                    fel.WriteLine += (string message) => Trace.WriteLine(message);
                    if (!fel.Open(isFel: false))
                    {
                        throw new Exception("USB Device Not Found");
                    }

                    if (!fel.UsbUpdateProbe())
                    {
                        throw new Exception("Failed to handshake with burn mode");
                    }

                    probeSuccess = true;

                    if (!fel.UsbUpdateEnterFel())
                    {
                        throw new Exception("Failed to enter FEL");
                    }

                    result = true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                    result = probeSuccess || false;
                }
                finally
                {
                    fel.Close();
                }
            }

            return result;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (Fel.DeviceExists())
            {
                timer.Enabled = false;
                var handshakeSuccess = AttemptUpdateHandshake();

                new Thread(() =>
                {
                    if (handshakeSuccess)
                    {
                        Thread.Sleep(1000);

                        while (!Fel.DeviceExists())
                        {
                            Thread.Sleep(1000);
                        }
                    }

                    Invoke(new Action(() =>
                    {
                        DialogResult = DialogResult.OK;
                        deviceFound = true;
                        Close();
                    }));
                }).Start();
            }
        }

        private void WaitingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!deviceFound && !Fel.DeviceExists())
            {
                if (Tasks.MessageForm.Show(Resources.AreYouSure, Resources.DoYouWantCancel, Resources.sign_warning, new Tasks.MessageForm.Button[] { Tasks.MessageForm.Button.Yes, Tasks.MessageForm.Button.No }, Tasks.MessageForm.DefaultButton.Button2) == Tasks.MessageForm.Button.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    DialogResult = DialogResult.Abort;
                }
            }
        }

        private void buttonDriver_Click(object sender, EventArgs e)
        {
            try
            {
                buttonDriver.Enabled = InstallDriver() != 0;
            }
            catch (Exception ex)
            {
                Tasks.ErrorForm.Show(this, ex);
            }
        }

        private void WaitingForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            timer.Enabled = false;
        }

        private void WaitingFelForm_Load(object sender, EventArgs e)
        {

        }
    }
}


