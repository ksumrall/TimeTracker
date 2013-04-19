using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace TimeTracker
{
    [RunInstaller(true)]
    public partial class _Installer1 : System.Configuration.Install.Installer
    {
        public _Installer1()
        {
            ServiceProcessInstaller process = new ServiceProcessInstaller();
            process.Account = ServiceAccount.LocalSystem;

            ServiceInstaller serviceAdmin = new ServiceInstaller();
            serviceAdmin.StartType = ServiceStartMode.Manual;
            serviceAdmin.ServiceName = "TimeTracker";
            serviceAdmin.DisplayName = "Time Tracking Application";

            Installers.Add(process);
            Installers.Add(serviceAdmin);
        }
    }
}
