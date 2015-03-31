using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;

namespace NancyHostLib
{
    public class SystemGlobals
    {
        private static volatile bool _initialized = false;

        public readonly static CultureInfo CultureBr = new CultureInfo ("pt-BR");
        public readonly static CultureInfo CultureEn = new CultureInfo ("en");

        private static FlexibleOptions _options = new FlexibleOptions ();
        
        public static FlexibleOptions Options
        {
            get { return _options;  }
        }

        public static FlexibleOptions Initialize (string[] args = null)
        {
            if (_initialized)
                return Options;
            _initialized = true;

            // set culture info
            // net40 or lower
            // System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo ("en-US");
            // System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo ("en-US");
            // net45 or higher
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo ("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo ("en-US");

            // some additional configuration
            // http://stackoverflow.com/questions/8971210/how-to-turn-off-the-automatic-proxy-detection-in-the-amazons3-object
            System.Net.WebRequest.DefaultWebProxy = null;

            // increase limit of concurrent TCP connections
            // http://blogs.msdn.com/b/jpsanders/archive/2009/05/20/understanding-maxservicepointidletime-and-defaultconnectionlimit.aspx
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024; // more concurrent connections to the same IP (avoid throttling)
            System.Net.ServicePointManager.MaxServicePointIdleTime = 30 * 1000; // release unused connections sooner (15 seconds)

            // since mono blocks all non intalled SSL root certificate, lets disable it!
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };       
                        
            // log initialization
            InitializeLog ();

            // options initialization
            InitializeOptions (args);

            // load modules
            var folders = new List<string> ();
            try
            {
                if (!String.IsNullOrWhiteSpace (Options.Get ("Module")))
                    folders.Add (System.IO.Path.GetDirectoryName (Options.Get ("Module")) + "/");
            }
            catch (Exception ex)
            {
                LogError ("error parsing module: " + Options.Get ("Module"), ex);
            }
            if (!String.IsNullOrWhiteSpace (Options.Get ("ModulesFolder")))
                folders.Add (Options.Get ("ModulesFolder"));
            if (folders.Count == 0)
                folders.Add ("${basedir}");
            // load
            ModuleContainer.Instance.LoadModules (folders.ToArray ());

            LogWarning ("Initialize", "StartUp");

            return Options;
        }

        static string _logFileName;
        static string _logLevel;

        private static void InitializeLog (string logFileName = null, string logLevel = null)
        {
            // default parameters initialization from config file
            if (String.IsNullOrEmpty (logFileName))
                logFileName = Options.Get<string> ("LogFilename", "${basedir}/log/" + typeof (SystemGlobals).Namespace + ".log");
            if (String.IsNullOrEmpty (logLevel))
                logLevel = Options.Get ("LogLevel", "Info");

            // check if log was initialized with same options
            if (_logFileName == logFileName && _logLevel == logLevel)
                return;
            
            // save current log configuration
            _logFileName = logFileName;
            _logLevel = logLevel;

            // try to parse loglevel
            LogLevel currentLogLevel;
            try { currentLogLevel = LogLevel.FromString (logLevel); }
            catch { currentLogLevel = LogLevel.Info; }

            // prepare log configuration
            var config = new NLog.Config.LoggingConfiguration ();

            // console output
            if (!Console.IsOutputRedirected)
            {
                var consoleTarget = new NLog.Targets.ColoredConsoleTarget ();
                consoleTarget.Layout = "${longdate}\t${callsite}\t${level}\t${message}\t${onexception: \\:[Exception] ${exception:format=tostring}}";

                config.AddTarget ("console", consoleTarget);

                var rule1 = new NLog.Config.LoggingRule ("*", LogLevel.Trace, consoleTarget);
                config.LoggingRules.Add (rule1);
            }


            // file output
            var fileTarget = new NLog.Targets.FileTarget ();
            fileTarget.FileName = Options.Get ("LogFilename", "${basedir}/log/" + typeof (SystemGlobals).Namespace + ".log");
            fileTarget.Layout = "${longdate}\t${logger}\t${level}\t\"${message}${onexception: \t [Exception] ${exception:format=tostring}}\"";//${callsite}
            fileTarget.Layout = "${longdate}\t${callsite}\t${level}\t\"${message}${onexception: \t [Exception] ${exception:format=tostring}}\"";
            fileTarget.ConcurrentWrites = true;
            fileTarget.AutoFlush = true;
            fileTarget.KeepFileOpen = true;
            fileTarget.DeleteOldFileOnStartup = false;
            fileTarget.ArchiveAboveSize = 2 * 1024 * 1024;  // 2 Mb
            fileTarget.MaxArchiveFiles = 10;
            fileTarget.ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Date;
            fileTarget.ArchiveDateFormat = "yyyyMMdd_HHmmss";

            // set file output to be async
            var wrapper = new NLog.Targets.Wrappers.AsyncTargetWrapper (fileTarget);

            config.AddTarget ("file", wrapper);

            // configure log from configuration file            
            fileTarget.FileName = logFileName;
            var rule2 = new NLog.Config.LoggingRule ("*", currentLogLevel, fileTarget);
            config.LoggingRules.Add (rule2);

            NLog.LogManager.Configuration = config;
        }

        private static void InitializeOptions (string[] args)
        {
            try
            {
                if (_options == null)
                    _options = new FlexibleOptions ();
                // parse local configuration file
                // display the options listed in the configuration file                 
                try
                {
                    var appSettings = System.Configuration.ConfigurationManager.AppSettings;
                    foreach (var k in appSettings.AllKeys)
                    {
                        _options.Set (k, appSettings[k]);                    
                    }
                }
                catch (Exception appSettingsEx)
                {
                    LogManager.GetCurrentClassLogger ().Warn (appSettingsEx);
                }

                // parse arguments like: key=value
                var argsOptions =  ParseCommandLineArguments (args);

                // merge options (priority order: argsOptions > localOptions)
                _options = FlexibleOptions.Merge (_options, argsOptions);

                // adjust alias for web hosted configuration file
                if (String.IsNullOrEmpty (_options.Get ("config")))
                    _options.Set ("config", _options.Get ("s3ConfigurationPath", _options.Get ("webConfigurationFile")));

                // load and parse web hosted configuration file (priority order: argsOptions > localOptions)
                string externalConfigFile = _options.Get ("config", "");
                bool configAbortOnError = _options.Get ("configAbortOnError", false);
                if (!String.IsNullOrWhiteSpace (externalConfigFile))
                {
                    foreach (var file in externalConfigFile.Trim (' ', '\'', '"', '[', ']').Split (',', ';'))
                    {
                        LogManager.GetCurrentClassLogger ().Warn ("Loading configuration file from {0} ...", externalConfigFile);
                        _options = FlexibleOptions.Merge (_options, LoadExtenalConfigurationFile (file.Trim (' ', '\'', '"'), configAbortOnError));
                    }
                }

                // merge options with the following priority:
                // 1. console arguments
                // 2. web configuration file
                // 3. local configuration file (app.config or web.config)
                // note: we must merge again for argsOptions has higher priority than all other
                _options = FlexibleOptions.Merge (_options, argsOptions);

                InitializeLog (_options.Get ("logFilename"), _options.Get ("logLevel", "Info"));
            }
            catch (Exception ex)
            {                
                LogManager.GetCurrentClassLogger ().Error (ex);
            }
        }

        private static FlexibleOptions ParseCommandLineArguments (string[] args)
        {
            var argsOptions = new FlexibleOptions ();
            if (args != null)
            {
                string arg;
                string lastTag = null;
                for (int ix = 0; ix < args.Length; ix++)
                {
                    arg = args[ix];
                    // check for option with key=value sintax
                    // also valid for --key:value
                    int p = arg.IndexOf ('=');
                    if (p > 0)
                    {
                        argsOptions.Set (arg.Substring (0, p).Trim ().TrimStart ('-', '/'), arg.Substring (p + 1).Trim ());
                        lastTag = null;
                        continue;
                    }

                    // search for tag stating with special character
                    if (arg.StartsWith ("-", StringComparison.Ordinal) || arg.StartsWith ("/", StringComparison.Ordinal))
                    {
                        lastTag = arg.Trim ().TrimStart ('-', '/');
                        argsOptions.Set (lastTag, "true");
                        continue;
                    }

                    // set value of last tag
                    if (lastTag != null)
                    {
                        argsOptions.Set (lastTag, arg.Trim ());
                    }
                }
            }
            return argsOptions;
        }

        public static void LogInfo (string tag, string message, object pageContext = null)
        {
            NLog.LogManager.GetLogger (tag).Info (message);
        }

        public static void LogWarning (string tag, string message, object pageContext = null)
        {
            NLog.LogManager.GetLogger (tag).Warn (message);
        }

        public static void LogError (string tag, string message, object pageContext = null)
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

        public static void LogDebug (string tag, string message, object pageContext = null)
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

        private static FlexibleOptions LoadExtenalConfigurationFile (string filePath, bool thrownOnError)
        {
            if (filePath.StartsWith ("http", StringComparison.OrdinalIgnoreCase))
            {
                return LoadWebConfigurationFile (filePath, thrownOnError);
            }
            else
            {
                return LoadFileSystemConfigurationFile (filePath, thrownOnError);
            }
        }

        private static FlexibleOptions LoadWebConfigurationFile (string filePath, bool thrownOnError)
        {
            var options = new FlexibleOptions ();
            using (WebClient client = new WebClient ())
            {
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse (client.DownloadString (filePath));
                    foreach (var i in json)
                    {
                        options.Set (i.Key, i.Value.ToString (Newtonsoft.Json.Formatting.None));
                    }
                }
                catch (Exception ex)
                {
                    if (thrownOnError)
                        throw;
                    LogManager.GetCurrentClassLogger ().Error (ex);
                }
            }
            return options;
        }

        private static FlexibleOptions LoadFileSystemConfigurationFile (string filePath, bool thrownOnError)
        {
            var options = new FlexibleOptions ();
            using (WebClient client = new WebClient ())
            {
                try
                {
                    string text;
                    using (var file = new System.IO.StreamReader (filePath, Encoding.GetEncoding ("IDO-8859-1"), true))
                    {
                        text = file.ReadToEnd ();
                    }
                    var json = Newtonsoft.Json.Linq.JObject.Parse (text);
                    foreach (var i in json)
                    {
                        options.Set (i.Key, i.Value.ToString (Newtonsoft.Json.Formatting.None));
                    }
                }
                catch (Exception ex)
                {
                    if (thrownOnError)
                        throw;
                    LogManager.GetCurrentClassLogger ().Error (ex);
                }
            }
            return options;
        }
    }

    public class FlexibleOptions
    {
        private Dictionary<string, string> _options;

        /// <summary>
        /// Internal dictionary with all options.
        /// </summary>
        public Dictionary<string, string> Options
        {
            get
            {
                if (_options == null)
                    _options = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
                return _options;
            }
            set { _options = value; }
        }

        /// <summary>
        /// Check if a key exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool HasOption (string key)
        {
            return Options.ContainsKey (key);
        }

        /// <summary>
        /// Add/Overwrite and option. The value will be serialized to string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public FlexibleOptions Set<T> (string key, T value)
        {
            if (key != null)
            {
                bool isNull = (value as object == null);
                if (typeof (T) == typeof (string) && !isNull)
                {
                    Options[key] = (string)(object)value;
                }
                else if (typeof (T).IsPrimitive)
                {
                    Options[key] = (string)Convert.ChangeType (value, typeof (string), System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (!isNull)
                {
                    Options[key] = Newtonsoft.Json.JsonConvert.SerializeObject (value);
                }
            }
            return this;
        }

        /// <summary>
        /// Get the option as string. If the key doen't exist, an Empty string is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get (string key)
        {
            return Get<string> (key, String.Empty);
        }

        /// <summary>
        /// Get the option as the desired type.
        /// If the key doen't exist or the type convertion fails, the provided defaultValue is returned.
        /// The type convertion uses the Json.Net serialization to try to convert.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T Get<T> (string key, T defaultValue)
        {
            string v;
            if (key != null && Options.TryGetValue (key, out v))
            {
                try
                {
                    if (v == null || v.Length == 0)
                        return defaultValue;

                    bool missingQuotes = v.Length < 2 || (!(v[0] == '\"' && v[v.Length - 1] == '\"'));
                    var desiredType = typeof (T);

                    if (desiredType == typeof (string))
                    {
                        if (missingQuotes)
                            return (T)(object)v;
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                    }
                    // else, use a type convertion with InvariantCulture (faster)
                    else if (desiredType.IsPrimitive)
                    {
                        if (!missingQuotes)
                            v = v.Substring (1, v.Length - 2);
                        return (T)Convert.ChangeType (v, typeof (T), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    // more comprehensive datetime parser, except formats like "\"\\/Date(1335205592410-0500)\\/\""
                    else if ((desiredType == typeof (DateTime) || desiredType == typeof (DateTime?)) && v.IndexOf ('(', 4, 10) < 0)
                    {
                        DateTime dt;
                        if (DateTime.TryParse (missingQuotes ? v : v.Substring (1, v.Length - 2), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                            return (T)(object)dt;
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                    }
                    else if (desiredType == typeof (Guid) || desiredType == typeof (Guid?))
                    {
                        Guid guid;
                        if (Guid.TryParse (v, out guid))
                            return (T)(object)guid;
                    }
                    else if (desiredType == typeof (TimeSpan) || desiredType == typeof (TimeSpan?))
                    {
                        TimeSpan timespan;
                        if (TimeSpan.TryParse (Convert.ToString (v), out timespan))
                            return (T)(object)timespan;
                    }

                    // finally, deserialize it!                   
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T> (v);
                }
                catch { /* ignore and return default value */ }
            }
            return defaultValue;
        }

        /// <summary>
        /// Merge together FlexibleObjects, the last object in the list has priority in conflict resolution (overwrite).
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static FlexibleOptions Merge (params FlexibleOptions[] items)
        {
            var merge = new FlexibleOptions ();
            if (items != null)
            {
                foreach (var i in items)
                {
                    if (i == null)
                        continue;
                    foreach (var o in i.Options)
                    {
                        merge.Options[o.Key] = o.Value;
                    }
                }
            }
            return merge;
        }
    }
}