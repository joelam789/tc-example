using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace SimpleSharpServer
{
    [RunInstaller(true)]
    public partial class SimpleSharpServerServiceInstaller : System.Configuration.Install.Installer
    {
        public SimpleSharpServerServiceInstaller()
        {
            InitializeComponent();

            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            //set the privileges
            processInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller.DisplayName = Program.SVC_NAME;
            serviceInstaller.Description = Program.SVC_DESC;
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            //must be the same as what was set in Program's constructor
            serviceInstaller.ServiceName = Program.SVC_NAME;
            this.Installers.Add(processInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}
