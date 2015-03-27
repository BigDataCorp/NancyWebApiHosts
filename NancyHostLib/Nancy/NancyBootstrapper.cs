using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NancyHostLib
{
    public class NancyBootstrapper : DefaultNancyBootstrapper
    {
        static Tuple<string, decimal>[] DefaultEmptyHeader = new[] { Tuple.Create ("application/json", 1.0m), Tuple.Create ("text/html", 0.9m), Tuple.Create ("*/*", 0.8m) };

        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger ();

        static bool enableAuthentication = false;

        static NancyBootstrapper()
        {
            
        }
        protected override void ConfigureConventions (Nancy.Conventions.NancyConventions nancyConventions)
        {
            // add resorce folders (where javascript, images, css etc. can be served)
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("lib", @"lib"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("Content", @"Content"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("fonts", @"fonts"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("Scripts", @"Scripts"));
            nancyConventions.StaticContentsConventions.Add (Nancy.Conventions.StaticContentConventionBuilder.AddDirectory ("images", @"images"));

            // add some default header Accept if empty (to provide a degault dynamic content negotiation rule)
            this.Conventions.AcceptHeaderCoercionConventions.Add ((acceptHeaders, ctx) =>
            {
                if (!acceptHeaders.Any ())
                {
                    return DefaultEmptyHeader;
                }
                return acceptHeaders;
            });

            base.ConfigureConventions (nancyConventions);
        }

        protected override void ApplicationStartup (Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            // configure nancy
            Nancy.Diagnostics.DiagnosticsHook.Disable (pipelines);
            StaticConfiguration.CaseSensitive = false;
            StaticConfiguration.DisableErrorTraces = true;
            StaticConfiguration.EnableRequestTracing = false;
            Nancy.Json.JsonSettings.MaxJsonLength = 20 * 1024 * 1024;
            
            // log any errors
            pipelines.OnError.AddItemToStartOfPipeline ((ctx, ex) =>
            {
                logger.Error (ex);
                return null;
            });

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
                    // to improve thethrouput, lets signal the client to close and reopen the connection per request
                    ctx.Response.Headers["Connection"] = "close";
                }
            });

            // global authentication
            enableAuthentication = SystemGlobals.Options.Get ("EnableAuthentication", false);
            pipelines.BeforeRequest.AddItemToStartOfPipeline (AllResourcesAuthentication);

            // gzip compression            
            pipelines.AfterRequest.AddItemToEndOfPipeline (NancyCompressionExtenstion.CheckForCompression);
            
            base.ApplicationStartup (container, pipelines);            
        }

        protected override void RequestStartup(Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines, NancyContext context)
        {            
            base.RequestStartup (container, pipelines, context);
        }

        #region *   Authentication  *

        BDCAccessControlClient.AccessControl accessControlContext = new BDCAccessControlClient.AccessControl ();

        /// <summary>
        /// Allow anonymous access only to the login page
        /// </summary>        
        private Response AllResourcesAuthentication (NancyContext ctx)
        {
            // if authenticated, go on...
            if (ctx.CurrentUser != null)
                return null;
            // if authentication is disbled, go on...
            if (!enableAuthentication)
                return null;
            
            // if login module, go on... (here we can put other routes without authentication)
            if (ctx.Request.Url.Path.IndexOf ("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            // search for a session id or token
            BDCAccessControlClient.UserInfoResponse user = null;
            
            // 1. check for token authentication: Header["Authorization"] with the sessionId/token 
            string authToken = ctx.Request.Headers.Authorization;
            if (authToken != null && authToken.Length > 0)
            {
                user = accessControlContext.GetUser (authToken);
                if (user.Result)
                    ctx.CurrentUser = new UserIdentityModel (authToken, user.UserInfo);
            }

            // 2. check for token authentication: query parameter or form unencoded parameter
            if (ctx.CurrentUser == null)
            {
                authToken = TryGetRequestParameter (ctx, "token");
                if (authToken != null && authToken.Length > 0)
                {
                    user = accessControlContext.GetUser (authToken);
                    if (user.Result)
                        ctx.CurrentUser = new UserIdentityModel (authToken, user.UserInfo);
                }
            }

            // 3. check login/password
            if (ctx.CurrentUser == null)
            {
                var password = TryGetRequestParameter (ctx, "password");
                var login = TryGetRequestParameter (ctx, "login");
                if (!String.IsNullOrEmpty (password) && !String.IsNullOrEmpty (login))
                {
                    user = accessControlContext.OpenSession (login, password, 60);
                    if (user.Result)
                        ctx.CurrentUser = new UserIdentityModel (user.UserInfo.SessionId, user.UserInfo);
                }
            }

            // analise if we got an authenticated user            
            return (ctx.CurrentUser == null) ? new Nancy.Responses.HtmlResponse (HttpStatusCode.Unauthorized) : null;
        }

        #endregion
        
        #region *   JSON serialization options  *
        /// ***********************
        /// Custom Json net options
        /// ***********************
        public class CustomJsonSerializer : JsonSerializer
        {
            public static JsonSerializerSettings DefaultNewtonsoftJsonSettings;
            public CustomJsonSerializer ()
            {
                Formatting = Formatting.None;
                MissingMemberHandling = MissingMemberHandling.Ignore;
                NullValueHandling = NullValueHandling.Ignore;
                ObjectCreationHandling = ObjectCreationHandling.Replace;
            }
        }

        // Add this converter to the default settings of Newtonsoft JSON.NET.            
        protected override void ConfigureApplicationContainer (Nancy.TinyIoc.TinyIoCContainer container)        
        {
            base.ConfigureApplicationContainer (container);

            container.Register<JsonSerializer, CustomJsonSerializer> ();
        }

        #endregion

        #region *   Helper methods  *
        /// ***********************
        /// Helper methods
        /// ***********************
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
