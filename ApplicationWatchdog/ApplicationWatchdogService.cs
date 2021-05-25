using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ApplicationWatchdog
{
    public partial class ApplicationWatchdogService : ServiceBase
    {
        public ApplicationWatchdogService()
        {
            InitializeComponent();
        }

        internal void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            Core.Controller.Start();
        }

        protected override void OnStop()
        {
            Core.Controller.Stop();
        }
    }
}
