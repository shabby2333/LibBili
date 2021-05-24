using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LibBili.Danmaku.Util
{
    public static class Util
    {
        public static byte[] ToBigEndian(this byte[] b)
        {
            return BitConverter.IsLittleEndian ? b.Reverse().ToArray() : b;
        }
    }
}
