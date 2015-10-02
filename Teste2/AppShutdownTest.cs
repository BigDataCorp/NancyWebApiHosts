using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teste2
{
    public class AppShutdownTest : NancyApiHost.Interfaces.IApplicationShutdown
    {
        public void Finalize ()
        {
            NLog.LogManager.GetLogger ("AppShutdownTest").Warn ("Shutdown requested");
        }
    }
}
