using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            dynamic user = ((dynamic)Context.CurrentUser).User;
            // we can also cast Dictionary to get our parameters!
            var parameters = (Dictionary<string,string>)user.Parameters;

            // let's format this user to display on screen !
            return user;
        }

        private dynamic showUserAsHtml (dynamic p)
        {
            // check if we have an authenticated user
            if (Context.CurrentUser == null)
                return "User Not Authenticated";

            // use dynamic to access our user!
            dynamic user = ((dynamic)Context.CurrentUser).User;
            // we can also cast Dictionary to get our parameters!
            var parameters = (Dictionary<string, string>)user.Parameters;

            // let's format this user to display on screen !
            return "Login: " + user.Login +
                   "<br/>Parameters: <br/> &nbsp; &nbsp; " +
                   String.Join ("<br/> &nbsp; &nbsp; ", parameters.Select (i => i.Key + ": " + i.Value));
        }
    }
}
