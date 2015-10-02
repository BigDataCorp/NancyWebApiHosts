using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost.Interfaces
{
    /// <summary>
    /// Provides a hook to execute code during application shutdown.
    /// </summary>
    public interface IApplicationShutdown
    {
        /// <summary>
        /// Perform any finalization tasks
        /// </summary>
        void Finalize ();
    }
}
