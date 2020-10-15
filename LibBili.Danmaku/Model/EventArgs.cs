using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibBili.Danmaku.Model
{

    public class ReceiveNoticeEventArgs: EventArgs
    {
        public string RawMessage { get; set; }
        public JObject JsonMessage { get; set; }
    }

    public class ReceiveDanmakuEventArgs: ReceiveNoticeEventArgs
    {
        public Danmaku Danmaku { get; set; }
    }

    public class ReceiveGiftEventArgs: ReceiveNoticeEventArgs
    {
        public Gift Gift { get; set; }
    }
}
