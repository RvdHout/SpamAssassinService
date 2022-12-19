
namespace SpamAssassinService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SpamAssassinServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.SpamAssassinServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // SpamAssassinServiceProcessInstaller
            // 
            this.SpamAssassinServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.SpamAssassinServiceProcessInstaller.Password = null;
            this.SpamAssassinServiceProcessInstaller.Username = null;
            // 
            // SpamAssassinServiceInstaller
            // 
            // this.SpamAssassinServiceInstaller.ServicesDependedOn = new string[] { "LanmanServer" };
            // this.SpamAssassinServiceInstaller.ServicesDependedOn = new string[] { "Tcpip", "Dhcp", "Dnscache" };
            this.SpamAssassinServiceInstaller.ServiceName = "SpamAssassin";
            this.SpamAssassinServiceInstaller.DisplayName = "SpamAssassin Daemon Control (spamd.exe) Service";
            this.SpamAssassinServiceInstaller.Description = "This system service controls the SpamAssassin daemon (spamd.exe)";
            this.SpamAssassinServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            this.SpamAssassinServiceInstaller.Committed += new System.Configuration.Install.InstallEventHandler(this.SpamAssassinServiceInstaller_Committed);
            // this.SpamAssassinServiceInstaller.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.SpamAssassinServiceInstaller_AfterInstall);
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.SpamAssassinServiceProcessInstaller,
            this.SpamAssassinServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller SpamAssassinServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller SpamAssassinServiceInstaller;
    }
}