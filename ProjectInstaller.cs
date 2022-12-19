#region using

using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

#endregion

namespace SpamAssassinService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void SpamAssassinServiceInstaller_Committed(object sender, InstallEventArgs e)
        {
            ServiceInstaller SpamAssassinServiceInstaller = (ServiceInstaller)sender;
            using (ServiceController sc = new ServiceController(SpamAssassinServiceInstaller.ServiceName))
            {
                sc.Start();
            }
        }

        private void SpamAssassinServiceInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            ServiceInstaller SpamAssassinServiceInstaller = (ServiceInstaller)sender;
            using (ServiceController sc = new ServiceController(SpamAssassinServiceInstaller.ServiceName))
            {
                sc.Start();
            }
        }
    }
}
