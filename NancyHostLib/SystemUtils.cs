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
            var shadowFolders = folders.SelectMany (f => System.IO.Directory.GetDirectories (f).Select (n => n.Replace ("/", "\\"))).ToList ();
            
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
                        shadowFolders.Add (module);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError ("error parsing module: " + Options.Get ("Module"), ex);
            }            

            // adjust appdomain shadow copy config (only for IIS asp.net hosting) 
            try
            {
#pragma warning disable 0618
                AppDomain.CurrentDomain.SetupInformation.ShadowCopyFiles = "true";            
                var shadowCopyPath = ((AppDomain.CurrentDomain.SetupInformation.ShadowCopyDirectories ?? "") + ";" + String.Join (";", shadowFolders)).Trim (';');
                AppDomain.CurrentDomain.SetShadowCopyPath (shadowCopyPath);
#pragma warning restore 0618
            }
            catch (Exception ex)
            {
                LogError ("error setting shadow copy paths", ex);
            }

            // load
            ModuleContainer.Instance.LoadModules (folders.ToArray ());

            LogWarning ("Initialize", "StartUp");

            return Options;
        }

        public static void Finalize ()
        {
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
        
        public static void LogInfo (string tag, string message)
        {
            NLog.LogManager.GetLogger (tag).Info (message);
        }

        public static void LogWarning (string tag, string message)
        {
            NLog.LogManager.GetLogger (tag).Warn (message);
        }

        public static void LogError (string tag, string message)
        {
            NLog.LogManager.GetLogger (tag).Error (message);
        }

        public static void LogError (string tag, Exception ex)
        {
            ex = ex.InnerException != null ? ex.InnerException : ex;
            string msg;
            if (ex.TargetSite != null)
            {
                msg = ex.TargetSite.DeclaringType.FullName;
            }
            else
            {
                var callingMethod = new System.Diagnostics.StackFrame (1).GetMethod ();
                msg = callingMethod.DeclaringType.Name + "." + callingMethod.Name;
            }
            NLog.LogManager.GetLogger (tag).Error (ex, msg);
        }

        public static void LogError (Exception ex)
        {
            ex = ex.InnerException != null ? ex.InnerException : ex;
            string msg;
            if (ex.TargetSite != null)
            {
                msg = ex.TargetSite.DeclaringType.FullName;
            }
            else
            {
                var callingMethod = new System.Diagnostics.StackFrame (1).GetMethod ();
                msg = callingMethod.DeclaringType.Name + "." + callingMethod.Name;
            }
            NLog.LogManager.GetCurrentClassLogger ().Error (ex, msg);
        }

        public static void LogDebug (string tag, string message)
        {
            NLog.LogManager.GetLogger (tag).Debug (message);
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