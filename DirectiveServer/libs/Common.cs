using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectiveServer.libs
{
    public static class Common
    {
        public static string BytesToString(byte[] bytes)
        {
            var str = "";

            for (var i = 0; i < bytes.Length; i++)
            {
                str += Convert.ToString(bytes[i], 16).PadLeft(2, '0') + ",";
            }

            return str;
        }
    }
}
