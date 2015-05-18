using NancyApiHost.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost
{
    public class SystemGlobals
    {
        private static FlexibleOptions _options;

        public static FlexibleOptions Options
        {
            get
            {
                if (_options == null)
                    _options = new FlexibleOptions ();
                return _options;
            }
            set { _options = value; }
        }
    }
}
