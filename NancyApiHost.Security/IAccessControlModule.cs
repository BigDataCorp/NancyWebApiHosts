using NancyApiHost.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost.Security
{
    public interface IAccessControlModule
    {
        void Initialize (FlexibleObject systemOptions);

        NancyHostUser GetUserFromToken (string token);

        string OpenSession (string userName, string password, TimeSpan? duration);
    }    
}
