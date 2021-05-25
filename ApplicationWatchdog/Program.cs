using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationWatchdog
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            //#if DEBUG
            //ApplicationWatchdogService aws = new ApplicationWatchdogService();
            //aws.OnDebug();
            //#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new ApplicationWatchdogService()
            };
            ServiceBase.Run(ServicesToRun);
            //#endif
        }
    }
}
