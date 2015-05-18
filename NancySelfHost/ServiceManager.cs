using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NancyHostLib;

namespace NancySelfHost
{
    public class ServiceManager
    {
        // Service Name as will be registered in the services control manager.
        // IMPORTANT: 
        // 1. should not contains spaces or other whitespace characters
        // 2. each service on the system must have a unique name.
        public const string DefaultServiceName = "BigDataNancySelfHost";

        // Display Name of the service in the services control manager.
        // NOTE: here the name can contains spaces or other whitespace characters
        public const string DefaultServiceDisplayName = "BigData NancySelfHost";

        // Description of the service in the services control manager.
        public const string DefaultServiceDescription = "BigData NancySelfHost service";

        private Logger _logger = LogManager.GetLogger ("NancySelfHost");
        private System.Threading.Timer _runningTask = null;
        private static int _running = 0;

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start ()
        {
            try
            {
                // startup setup
                InitializeWebInterface ();

                // Star Internal timer
                StartTimer ();
                _logger.Warn ("Service start");
            }
            catch (Exception ex)
            {
                _logger.Fatal (ex);
                throw ex;
            }
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop ()
        {
            try
            {
                StopTimer ();
                _logger.Warn ("Service stopped");
            }
            catch (Exception ex)
            {
                _logger.Error (ex);
            }
            LogManager.Flush ();
        }

        /// <summary>
        /// Pauses the service by stoping the internal timer.
        /// </summary>
        public void Pause ()
        {
            StopTimer ();
        }

        /// <summary>
        /// Continues this instance by restarting the internal timer.
        /// </summary>
        public void Continue ()
        {
            StartTimer ();
        }

        /// <summary>
        /// Starts the internal timer.
        /// </summary>
        private void StartTimer ()
        {

        }

        /// <summary>
        /// Stops the internal timer.
        /// </summary>
        private void StopTimer ()
        {

        }

        private void InitializeWebInterface ()
        {
            WebServer.Start (SystemUtils.Options.Get<int> ("webInterfacePort", 8080), null, SystemUtils.Options.Get ("webVirtualDirectoryPath", "/nancyselfhost"), SystemUtils.Options.Get ("webOpenFirewallExceptions", false));
            if (Environment.UserInteractive && SystemUtils.Options.Get<bool> ("webInterfaceDisplayOnBrowserOnStart", false))
            {
                DisplayPageOnBrowser (WebServer.Address);
            }

            //_logger.Warn (Newtonsoft.Json.JsonConvert.SerializeObject (AppDomain.CurrentDomain.GetAssemblies ().Where (a => (!a.GlobalAssemblyCache && !a.IsDynamic)).Select (i => i.FullName), Newtonsoft.Json.Formatting.Indented));
        }

        /// <summary>
        /// Adjust file path.
        /// </summary>
        private string prepareFilePath (string path)
        {
            if (path == null)
                return path;
            int ix = path.IndexOf ("${basedir}", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                int tagLen = "${basedir}".Length;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (path.Length > ix + tagLen && (path[ix + tagLen] == '\\' || path[ix + tagLen] == '/'))
                    appDir = appDir.EndsWith ("/") || appDir.EndsWith ("\\") ? appDir.Substring (0, appDir.Length - 1) : appDir;
                path = path.Remove (ix, tagLen);
                path = path.Insert (ix, appDir);
            }
            path = path.Replace ("\\", "/");
            return path;
        }

        
        /// <summary>
        /// Displays the pipeline web UI on browser.
        /// </summary>
        private void DisplayPageOnBrowser (string address)
        {
            Task.Factory.StartNew (() =>
            {
                try
                {
                    System.Diagnostics.Process.Start (address);
                }
                catch (Exception ignore)
                {
                    _logger.Info (ignore);
                }
            });
        }

    }

}
