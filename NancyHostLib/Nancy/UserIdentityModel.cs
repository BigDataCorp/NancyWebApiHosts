using Nancy.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NancyHostLib
{
    public class UserIdentityModel : IUserIdentity
    {
        public UserIdentityModel ()
        {}

        public UserIdentityModel (string token, BDCAccessControlClient.User user)
        {
            UserName = user.Login;
            User = user;
            SessionId = token;
        }

        public IEnumerable<string> Claims { get; set; }

        public string UserName { get; set; }

        public BDCAccessControlClient.User User { get; set; }

        public string SessionId { get; set; }
    }
}