using NancyApiHost.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost.Security
{
    public class AccessControlFactory
    {
        static Func<object> _ctr;

        public static void RegisterFactory (Func<object> ctr)
        {
            _ctr = ctr;            
        }

        public static IAccessControlModule Create ()
        {
            if (_ctr != null)
                return (IAccessControlModule)_ctr ();
            return null;
        }
    }
}
