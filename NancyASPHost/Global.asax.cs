using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;

namespace NancyASPHost
{
    public class Global : System.Web.HttpApplication
    {

        protected void Application_Start (object sender, EventArgs e)
        {
            // ensure system globals is initialized
            NancyHostLib.SystemUtils.Initialize (true);
        }

        protected void Session_Start (object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest (object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest (object sender, EventArgs e)
        {

        }

        protected void Application_Error (object sender, EventArgs e)
        {

        }

        protected void Session_End (object sender, EventArgs e)
        {

        }

        protected void Application_End (object sender, EventArgs e)
        {
            NancyHostLib.SystemUtils.Finalize ();
        }
    }
}