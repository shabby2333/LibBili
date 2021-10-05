using System;
using System.Collections.Generic;
using System.Text;

namespace LibBili.Danmaku
{
    public struct Gift
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string GiftName { get; set; }
        public int GiftCount { get; set; }
    }
}
