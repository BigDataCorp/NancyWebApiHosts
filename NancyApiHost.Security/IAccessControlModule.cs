using NancyApiHost.Interfaces.SimpleHelpers;
using System;

namespace NancyApiHost.Security
{
    public interface IAccessControlModule
    {
        void Initialize (FlexibleObject systemOptions);

        NancyHostUser GetUserFromToken (string token);

        string OpenSession (string userName, string password, TimeSpan? duration);
    }    
}
