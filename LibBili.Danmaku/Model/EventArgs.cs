using System;
using System.Text.Json.Nodes;

namespace LibBili.Danmaku.Model
{

    public class ReceiveNoticeEventArgs: EventArgs
    {
        public string RawMessage { get; set; }
        public JsonNode JsonMessage { get; set; }
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
