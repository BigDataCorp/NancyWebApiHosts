using NancyApiHost.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NancyApiHost.SimpleHelpers;

namespace BDCSecurityModule
{
    public class BDCAccessControl : IAccessControlModule
    {
        public void Initialize (FlexibleObject systemOptions)
        {
            BDCAccessControlClient.AccessControl.BaseUrl = systemOptions.Get ("AccessControlBaseUrl", BDCAccessControlClient.AccessControl.BaseUrl);
        }

        public NancyHostUser GetUserFromToken (string token)
        {
            var ctrl = new BDCAccessControlClient.AccessControl ();
            var user = ctrl.GetUser (token);
            if (!user.Result || user.UserInfo == null)
                return null;
            
            var hostUser = new NancyHostUser 
            {
                UserName = user.UserInfo.Login,
                Group = user.UserInfo.Group
            };

            foreach (var i in user.UserInfo.Parameters)
                hostUser.Options.Set (i.Key, i.Value);

            return hostUser;
        }

        public string OpenSession (string userName, string password, TimeSpan? duration)
        {
            var ctrl = new BDCAccessControlClient.AccessControl ();
            int durationInMinutes = 8 * 60;
            if (duration != null && duration.HasValue && duration.Value.TotalSeconds > 1)
                durationInMinutes = (int)duration.Value.TotalMinutes;
            var user = ctrl.OpenSession (userName, password, durationInMinutes);
            if (!user.Result || user.UserInfo == null)
                return null;
            return user.UserInfo.SessionId;
        }
    }
}
