using NancyApiHost;
using NancyApiHost.SimpleHelpers;
using NancyHostLib.SimpleHelpers;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace NancyHostLib
{
    public class SystemUtils
    {
        private static volatile bool _initialized = false;

        public readonly static CultureInfo CultureBr = new CultureInfo ("pt-BR");
        public readonly static CultureInfo CultureEn = new CultureInfo ("en");

        private static FlexibleOptions _options;
        
        public static FlexibleOptions Options
        {
            get { return _options;  }
        }

        public static FlexibleOptions Initialize (string[] args = null)
        {
            return Initialize (false, args);
        }

        public static FlexibleOptions Initialize (bool useShadowCopy, string[] args = null)
        {
            if (_initialized)
                return Options;
            _initialized = true;

            // initilize log, arguments, etc...
            _options = ConsoleUtils.Initialize (args, false);
            SystemGlobals.Options = _options;

            // get modules paths
            var folders = new HashSet<string> (
                Options.Get ("modulesFolder", "").Split (',', ';', '|')
                    .Concat (Options.Get ("modules", "").Split (',', ';', '|')).Where (i => !String.IsNullOrEmpty (i)).Select (i => prepareFilePath (i)).Where (i => System.IO.Directory.Exists (i)),
                StringComparer.OrdinalIgnoreCase);
            
            // generate folders to be shadow copied
            List<string> shadowFolders = null;
            if (useShadowCopy)
                shadowFolders = folders.SelectMany (f => System.IO.Directory.EnumerateFiles (f, "*.dll", System.IO.SearchOption.AllDirectories).Select (i => System.IO.Path.GetDirectoryName (i)).Distinct ()).ToList ();
            
            // add additional module paths
            folders.Add (prepareFilePath ("${basedir}"));
            
            // check if we also have the module argument
            try
            {
                var module = Options.Get ("Module");
                if (!String.IsNullOrWhiteSpace (module))
                {
                    if (!System.IO.Directory.Exists (module))
                        module = System.IO.Path.GetDirectoryName (module);
                    if (System.IO.Directory.Exists (module))
                    {
                        folders.Add (module);
                        if (useShadowCopy) shadowFolders.Add (module);
                    }
                }
            }
            catch (Exception ex)
            {
                GetLogger ().Error (ex, "error parsing module: " + Options.Get ("Module"));
            }            

            // adjust appdomain shadow copy config (only for IIS asp.net hosting) 
            if (useShadowCopy)
            {
                try
                {
                    #pragma warning disable 0618                
                    var shadowCopyPath = ((AppDomain.CurrentDomain.SetupInformation.ShadowCopyDirectories ?? "") + ";" + String.Join (";", shadowFolders)).Trim (';');
                    AppDomain.CurrentDomain.SetShadowCopyPath (shadowCopyPath);
                    AppDomain.CurrentDomain.SetShadowCopyFiles ();
                    #pragma warning restore 0618
                }
                catch (Exception ex)
                {
                    GetLogger ().Error (ex, "error setting shadow copy paths");
                }
            }

            // load
            var types = new Type[] { typeof (NancyApiHost.Security.IAccessControlModule), typeof (NancyApiHost.Interfaces.IApplicationShutdown) };
            ModuleContainer.Instance.LoadModules (folders.ToArray (), types);

            GetLogger ().Info ("Initialize", "StartUp");

            return Options;
        }

        public static void Finalize ()
        {
            try
            {
                foreach (var instance in ModuleContainer.Instance.GetInstancesOf<NancyApiHost.Interfaces.IApplicationShutdown> ())
                {
                    instance.Finalize ();
                }
            }
            catch (Exception ex)
            {
                GetLogger ().Error (ex);
            }
            NLog.LogManager.Flush ();
        }       

        /// <summary>
        /// Adjust file path.
        /// </summary>
        private static string prepareFilePath (string path)
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
        
        public static NLog.Logger GetLogger ()
        {
            return NLog.LogManager.GetLogger ("NancyHost");
        }        

        /// <summary>
        /// Gets the cookie sent by the browser or creates a new one (request cookie).
        /// </summary>
        public static string GetCookie (Nancy.NancyContext page, string cookieName)
        {
            string value;
            page.Request.Cookies.TryGetValue (cookieName, out value);
            return value;
        }

        /// <summary>
        /// Saves the cookie in the response.
        /// </summary>
        public static void SetCookie (Nancy.NancyContext page, string cookieName, string value)
        {
            SetCookie (page, cookieName, value, TimeSpan.FromDays (365));
        }

        /// <summary>
        /// Saves the cookie in the response.
        /// </summary>
        public static void SetCookie (Nancy.NancyContext page, string cookieName, string value, TimeSpan expiration)
        {
            page.Response.AddCookie (cookieName, value, DateTime.UtcNow.Add (expiration));
        }
        
    }
}