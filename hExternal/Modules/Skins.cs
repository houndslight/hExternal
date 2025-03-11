using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hExternal.Modules
{
    internal class Skins
    {
        public static Skins? skins;

        public static Skins Instance
        {
            get
            {
                if (skins == null)
                {
                    skins = new Skins();
                }
                return skins;
            }
        }

    }
}
