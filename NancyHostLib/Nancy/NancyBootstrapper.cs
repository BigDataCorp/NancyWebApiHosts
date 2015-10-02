using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NancyApiHost.Security;

namespace NancyHostLib
{
    public class NancyBootstrapper : DefaultNancyBootstrapper
    {
        static Tuple<string, decimal>[] DefaultEmptyHeader = new[] { Tuple.Create ("application/json", 1.0m), Tuple.Create ("text/html", 0.9m), Tuple.Create ("*/*", 0.8m) };

        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger ();

        static bool enableAuthentication = false;
        static HashSet<string> pathsAnonimousAccess = null;
        static HashSet<string> pathsAuthAccess = null;
        static bool debugMode = false;

        protected override void ConfigureConventions (Nancy.Conventions.NancyConventions nancyConventions)
        {
            // add resorce folders (where javascript, images, css etc. can be served)
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("Content", @"Content"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("Scripts", @"Scripts"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("images", @"images"));

            // add some default header Accept if empty (to provide a degault dynamic content negotiation rule)
            this.Conventions.AcceptHeaderCoercionConventions.Add ((acceptHeaders, ctx) =>
            {
                return acceptHeaders.Any () ? acceptHeaders : DefaultEmptyHeader;
            });

            base.ConfigureConventions (nancyConventions);
        }

        protected override void ApplicationStartup (Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            // configure nancy            
            StaticConfiguration.CaseSensitive = false;            
            Nancy.Json.JsonSettings.MaxJsonLength = 20 * 1024 * 1024;

            debugMode = SystemUtils.Options.Get ("debugMode", false);
            // check if the debugmode flag is enabled
            if (!debugMode)
            {
                Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);
                StaticConfiguration.DisableErrorTraces = true;
                StaticConfiguration.EnableRequestTracing = false;

                // log any errors only as debug 
                pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
                {
                    if (logger.IsTraceEnabled)
                        logger.Trace (ex);
                    return null;
                });
            }
            else
            {
                StaticConfiguration.DisableErrorTraces = false;
                StaticConfiguration.EnableRequestTracing = true;
                StaticConfiguration.Caching.EnableRuntimeViewDiscovery = true;
                StaticConfiguration.Caching.EnableRuntimeViewUpdates = true;                

                // log any errors as errors
                pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
                {
                    logger.Error (ex);
                    return null;
                });
            }

#if DEBUG
            StaticConfiguration.DisableErrorTraces = false;
            StaticConfiguration.EnableRequestTracing = true;
            StaticConfiguration.Caching.EnableRuntimeViewDiscovery = true;
            StaticConfiguration.Caching.EnableRuntimeViewUpdates = true;
#endif

            // in case there is a proxy using SSL routing request to nancy 
            // https://github.com/NancyFx/Nancy/wiki/SSL-Behind-Proxy
            if (SystemUtils.Options.Get ("SSLProxy", false))
            {
                Nancy.Security.SSLProxy.RewriteSchemeUsingForwardedHeaders(pipelines);
            }

            // https://github.com/NancyFx/Nancy/wiki/Model-binding
            Nancy.ModelBinding.BindingConfig.Default.IgnoreErrors = true;

            // some additional response configuration
            pipelines.AfterRequest.AddItemToEndOfPipeline ((ctx) =>
            {
                // CORS Enable
                if (ctx != null && ctx.Response != null && ctx.Response.ContentType != null &&
                    (ctx.Response.ContentType.Contains ("json") || ctx.Response.ContentType.Contains ("xml")))
                {
                    ctx.Response.WithHeader ("Access-Control-Allow-Origin", "*")
                        .WithHeader ("Access-Control-Allow-Methods", "POST,GET")
                        .WithHeader ("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
                }
            });

            // global authentication
            enableAuthentication = SystemUtils.Options.Get ("EnableAuthentication", SystemUtils.Options.Get ("Authentication", false));
            if (enableAuthentication)
            {
                pathsAnonimousAccess = new HashSet<string> (SystemUtils.Options.GetAsList ("PathsAnonimousAccess").Where (i => !String.IsNullOrWhiteSpace (i)).Select (i => PreparePathForAuthCheck (i.Trim ()).Trim ()), StringComparer.OrdinalIgnoreCase);
                pathsAuthAccess = new HashSet<string> (SystemUtils.Options.GetAsList ("PathsAuthAccess").Where (i => !String.IsNullOrWhiteSpace (i)).Select (i => PreparePathForAuthCheck (i.Trim ()).Trim ()), StringComparer.OrdinalIgnoreCase);
            }
            pipelines.BeforeRequest.AddItemToStartOfPipeline (AllResourcesAuthentication);
            accessControlContext = ModuleContainer.Instance.GetInstanceOf<IAccessControlModule> ();
            if (accessControlContext != null)
            {
                AccessControlFactory.RegisterFactory (ModuleContainer.Instance.GetConstructor (accessControlContext.GetType ()).Invoke);
            }

            // gzip compression
            pipelines.AfterRequest.AddItemToEndOfPipeline (NancyCompressionExtenstion.CheckForCompression);

            base.ApplicationStartup (container, pipelines);
        }

        protected override void RequestStartup (Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup (container, pipelines, context);
        }

        /// <summary>
        /// This method sets the password for the NancyFx diagnostics page when its is enabled
        /// To enable the diagnostics page: comment the disable call "Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);" at "ApplicationStartup" method
        /// To access diagnostics page: http://<address-of-your-application>/_Nancy/
        /// </summary>
        protected override Nancy.Diagnostics.DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new Nancy.Diagnostics.DiagnosticsConfiguration { Password = @"password" }; }
        }

        /// <summary>
        /// Lets adjust default container settings.
        /// Add this converter to the default settings of Newtonsoft JSON.NET.
        /// </summary>
        protected override void ConfigureApplicationContainer (Nancy.TinyIoc.TinyIoCContainer container)
        {
            //base.ConfigureApplicationContainer (container);

            // substitute nancy default assembly registration to a faster selected loading... (5x faster loading...)
            var nancyEngineAssembly = typeof (NancyEngine).Assembly;
            HashSet<string> blackListedAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "mscorlib", "vshost", "NLog", "Newtonsoft.Json", "Topshelf", "Topshelf.Linux", "Topshelf.NLog", "AWSSDK", "Dapper", "Mono.CSharp", "Mono.Security", "NCrontab", "Renci.SshNet", "System.Net.FtpClient", "MongoDB.Bson", "MongoDB.Driver", "System.Data.SQLite", "System.Net.Http.Formatting", "System.Web.Razor", "Microsoft.Owin.Hosting", "Microsoft.Owin", "Owin" };
            container.AutoRegister (AppDomain.CurrentDomain.GetAssemblies ().Where (a => !a.GlobalAssemblyCache && !a.IsDynamic && !blackListedAssemblies.Contains (ParseAssemblyName (a.FullName))), Nancy.TinyIoc.DuplicateImplementationActions.RegisterMultiple, t => t.Assembly != nancyEngineAssembly);

            // register json.net default options
            container.Register<JsonSerializer, CustomJsonSerializer> ();
        }

        /// ***********************
        /// Custom Path provider
        /// ***********************
        public class PathProvider : IRootPathProvider
        {
            static string _path = SetRootPath (AppDomain.CurrentDomain.BaseDirectory);//System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, @"site");//

            public string GetRootPath ()
            {
                return _path;
            }

            public static string SetRootPath (string fullPath)
            {
                fullPath = fullPath.Replace ('\\', '/');
                if (fullPath.Length == 0 || fullPath[fullPath.Length - 1] != '/')
                    fullPath += '/';
                if (fullPath.EndsWith ("/bin/", StringComparison.OrdinalIgnoreCase))
                {
                    var p = fullPath.LastIndexOf ("/bin/", StringComparison.OrdinalIgnoreCase);
                    fullPath = fullPath.Substring (0, p + 1);                    
                }
                _path = fullPath;
                return _path;
            }
        }

        protected override IRootPathProvider RootPathProvider
        {
            get { return new PathProvider (); }
        }

        #region *   Authentication  *

        IAccessControlModule accessControlContext = null;

        /// <summary>
        /// Allow anonymous access only to the login page
        /// </summary>        
        private Response AllResourcesAuthentication (NancyContext ctx)
        {
            // if authenticated, go on...
            if (ctx.CurrentUser != null)
                return null;
            
            // search for a session id or token
            if (accessControlContext == null)
                return null;

            // 1. check for token authentication: Header["Authorization"] with the sessionId/token 
            string authToken = ctx.Request.Headers.Authorization;
            if (authToken != null && authToken.Length > 0)
            {
                ctx.CurrentUser = accessControlContext.GetUserFromToken (authToken);
            }

            // 2. check for token authentication: query parameter or form unencoded parameter
            if (ctx.CurrentUser == null)
            {
                authToken = TryGetRequestParameter (ctx, "token");
                if (authToken != null && authToken.Length > 0)
                {
                    ctx.CurrentUser = accessControlContext.GetUserFromToken (authToken);
                }
            }

            // 3. finally, check if login and password were passed as parameters
            if (ctx.CurrentUser == null)
            {
                var password = TryGetRequestParameter (ctx, "password");                
                if (!String.IsNullOrEmpty (password))
                {
                    var login = TryGetRequestParameter (ctx, "login");
                    if (String.IsNullOrEmpty (login))
                        login = TryGetRequestParameter (ctx, "username");
                    if (!String.IsNullOrEmpty (login))
                        authToken = accessControlContext.OpenSession (login, password, TimeSpan.FromMinutes (60));
                    if (!String.IsNullOrEmpty (authToken))
                        ctx.CurrentUser = accessControlContext.GetUserFromToken (authToken);
                }
            }

            // check if we have an authenticated user
            if (ctx.CurrentUser != null)
                return null;

            // if authentication is disbled, go on...
            if (!enableAuthentication)
                return null;

            // check allowed and secure paths.
            var path = PreparePathForAuthCheck (ctx.Request.Url.Path);

            // routes without authentication
            if (pathsAnonimousAccess != null && pathsAnonimousAccess.Count > 0 && pathsAnonimousAccess.Contains (path))
                return null;

            // routes with required authentication
            if (pathsAuthAccess != null && pathsAuthAccess.Count > 0)            
                return pathsAuthAccess.Contains (path) ? new Nancy.Responses.HtmlResponse (HttpStatusCode.Unauthorized) : null;

            return new Nancy.Responses.HtmlResponse (HttpStatusCode.Unauthorized);            
        }
 
        private string PreparePathForAuthCheck (string path)
        {
            path = path.TrimEnd ('/');
            // keep the / for the base path only
            if (path.Length == 0) path = "/";
            // try to remove nancy format extension: .json, .xml
            var p = path.LastIndexOf ('/');
            if (p > 0)
            {
                var p1 = path.IndexOf ('.', p);
                if (p1 > 0)
                    path = path.Substring (0, p1);
            }
            return path;
        }

        #endregion

        #region *   JSON serialization options  *

        /// ***********************
        /// Custom Json net options
        /// ***********************
        public class CustomJsonSerializer : JsonSerializer
        {
            public CustomJsonSerializer ()
            {
                Formatting = Formatting.None;
                MissingMemberHandling = MissingMemberHandling.Ignore;
                NullValueHandling = NullValueHandling.Ignore;
                ObjectCreationHandling = ObjectCreationHandling.Replace;

                // note: this converter is added to the default settings of Newtonsoft JSON.NET in ConfigureApplicationContainer method
            }
        }

        #endregion

        #region *   Helper methods  *
        /// ***********************
        /// Helper methods
        /// ***********************
        private static string ParseAssemblyName (string name)
        {
            int i = name.IndexOf (',');
            return (i > 0) ? name.Substring (0, i) : name;
        }

        static string GetLastPathPart (string path)
        {
            // reduce length to disregard ending '\\' or '/'
            int len = path.Length - 2;
            if (len < 1)
                return String.Empty;
            int pos = path.LastIndexOf ('\\', len);
            if (pos < 0)
                pos = path.LastIndexOf ('/', len);
            if (pos > 0 && pos <= len)
                return path.Substring (pos + 1);
            return String.Empty;
        }

        static string PrepareFilePath (string path)
        {
            return (path ?? "").Replace ('\\', '/').Replace ("//", "/").Trim ('/');
        }

        static string TryGetRequestParameter (NancyContext ctx, string parameter)
        {
            object p;
            if (((DynamicDictionary)ctx.Request.Query).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            if (((DynamicDictionary)ctx.Request.Form).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            if (ctx.Parameters != null && ctx.Parameters is DynamicDictionary &&
                ((DynamicDictionary)ctx.Parameters).TryGetValue (parameter, out p) && p != null)
            {
                return p.ToString ();
            }

            return null;
        }

        #endregion

    }

}
