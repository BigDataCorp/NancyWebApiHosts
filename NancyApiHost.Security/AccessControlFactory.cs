using Nancy;
using NancyApiHost.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost.Security
{
    public class AccessControlFactory
    {
        static Func<object> _ctr;

        /// <summary>
        /// Registers the factory.
        /// </summary>
        /// <param name="ctr">The CTR.</param>
        public static void RegisterFactory (Func<object> ctr)
        {
            _ctr = ctr;            
        }

        /// <summary>
        /// Creates this instance.
        /// </summary>
        /// <returns></returns>
        public static IAccessControlModule Create ()
        {
            if (_ctr != null)
                return (IAccessControlModule)_ctr ();
            return null;
        }

        /// <summary>
        /// Gets the current user in NancyContext.
        /// </summary>
        /// <param name="ctx">Current NancyContext</param>
        /// <returns>NancyHostUser or null</returns>
        public static NancyHostUser GetUser (NancyContext ctx)
        {
            return GetUser (ctx, null, null);
        }

        /// <summary>
        /// Gets the user from the access control.<para/>
        /// This method is a shortcut that will open a session and get
        /// the session user from the current access control module.
        /// </summary>
        /// <param name="login">The login.</param>
        /// <param name="password">The password.</param>
        /// <returns>NancyHostUser or null</returns>
        public static NancyHostUser GetUser (string login, string password)
        {
            return GetUser (null, login, password);    
        }

        /// <summary>
        /// Gets the user from the access control.<para/>
        /// This method is a shortcut that will try to get the user from the nancy context.
        /// If there is no user on the context, it will open a new session and get
        /// the session user from the current access control module using the login and password.
        /// </summary>
        /// <param name="ctx">Current NancyContext</param>
        /// <param name="login">The login.</param>
        /// <param name="password">The password.</param>
        /// <returns>NancyHostUser or null</returns>
        public static NancyHostUser GetUser (NancyContext ctx, string login, string password)
        {
            if (ctx != null && ctx.CurrentUser != null)
            {
                return ctx.CurrentUser as NancyHostUser;
            }
            else if (login != null && password != null)
            {
                var ac = AccessControlFactory.Create ();
                if (ac != null)
                {
                    var token = ac.OpenSession (login, password, TimeSpan.FromHours (1));
                    if (token != null)
                        return ac.GetUserFromToken (token);
                }
            }
            return null;
        }
    }
}
