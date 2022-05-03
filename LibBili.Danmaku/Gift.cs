using System;
using System.Collections.Generic;

namespace LibBili.Danmaku
{
    public struct Gift
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string GiftName { get; set; }
        public int GiftCount { get; set; }
        public decimal Price { get; set; }
    }
}