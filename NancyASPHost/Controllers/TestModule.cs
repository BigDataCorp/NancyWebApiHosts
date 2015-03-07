using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NancyASPHost
{
    public class TestModule : NancyModule
    {
        public TestModule ()
        {
            Get["/"] = p => "Test Page";
        }
    }
}