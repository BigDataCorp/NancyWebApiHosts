using Nancy;
using NancyApiHost.Security;
using System;
using System.Linq;

namespace HealthCheckModule
{
    public class HealthCheck : NancyModule
    {
        public HealthCheck ()
            : base ("/HealthCheck")
        {
            Get["/"] = p => "Ok";
        }
    }
}
