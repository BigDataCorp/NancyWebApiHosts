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

            // load modules
            var folders = Options.Get ("modulesFolder", "").Split (',', ';', '|').Select (i => prepareFilePath (i))
                                 .Concat (Options.Get ("modules", "").Split (',', ';', '|'))
                                 .Concat (new string[] { "${basedir}/" })
                                 .Where (i => !String.IsNullOrEmpty (i)).ToArray ();
            try
            {
                if (!String.IsNullOrWhiteSpace (Options.Get ("Module")))
                {
                    folders = folders.Concat (new string[] { System.IO.Path.GetDirectoryName (Options.Get ("Module")) + "/" }).ToArray ();                    
                }
            }
            catch (Exception ex)
            {
                LogError ("error parsing module: " + Options.Get ("Module"), ex);
            }

            // load
            ModuleContainer.Instance.LoadModules (folders.ToArray ());

            LogWarning ("Initialize", "StartUp");

            return Options;
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
            NLog.LogManager.GetLogger (tag).Error (msg, ex);
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
            NLog.LogManager.GetCurrentClassLogger ().Error (msg, ex);
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