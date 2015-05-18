using Nancy;
using NancyApiHost.Security;
using System;
using System.Linq;

namespace Teste2
{
    public class TestModule : NancyModule
    {
        public TestModule ()
        {
            Get["/teste2"] = p => "Test Page - teste 2";

            Get["/showUser"] = showUser;
        }

        private dynamic showUser (dynamic p)
        {
            // check if we have an authenticated user
            if (Context.CurrentUser == null)
                return "User Not Authenticated";

            // use dynamic to access our user!
            var user = Context.CurrentUser as NancyHostUser;           

            // let's format this user to display on screen !
            return user;
        }

        private dynamic showUserAsHtml (dynamic p)
        {
            // check if we have an authenticated user
            if (Context.CurrentUser == null)
                return "User Not Authenticated";

            // use dynamic to access our user!
            var user = Context.CurrentUser as NancyHostUser;            

            // let's format this user to display on screen !
            return "Login: " + user.UserName +
                   "<br/>Parameters: <br/> &nbsp; &nbsp; " +
                   String.Join ("<br/> &nbsp; &nbsp; ", user.Options.Data.Select (i => i.Key + ": " + i.Value));
        }
    }
}
