using NancyApiHost.SimpleHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NancyApiHost.Security
{    
    public class NancyHostUser : Nancy.Security.IUserIdentity
    {
        private FlexibleObject _options;

        public string UserName { get; set; }

        public IEnumerable<string> Claims { get; set; }

        public string Group { get; set; }

        public FlexibleObject Options
        {
            get
            {
                if (_options == null)
                    _options = new FlexibleObject ();
                return _options;
            }
            set { _options = value; }
        }
    }
}
