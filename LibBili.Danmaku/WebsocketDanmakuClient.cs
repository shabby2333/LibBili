using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Websocket.Client;

namespace LibBili.Danmaku
{
    public class WebsocketDanmakuClient : BiliDanmakuClient
    {
        private const int WS_PORT = 2244;
        private const int WSS_PORT = 443;
        private const string DEFAULT_DANMAKU_URL = "hw-bj-live-comet-05.chat.bilibili.com";


        private WebsocketClient _ws;
        private string _url = "wss://hw-bj-live-comet-05.chat.bilibili.com/sub";

        public WebsocketDanmakuClient(long roomID, long? realRoomID = null) : base(roomID, realRoomID)
        {
        }

        public override async void Connect()
        {
            //尝试释放已连接的ws
            _ws?.Stop(WebSocketCloseStatus.Empty, string.Empty);
            _ws?.Dispose();

            if (!RealRoomID.HasValue)
            {
                // 汪姐姐说短房间号只有10000以下的，出问题记得点艹他
                // 汪姐姐又说短房间号只有5000以下的了，这下如果出问题必须狠狠的点屮她
                if (RoomID < 5000)
                {
                    var resp = await GetRoomInfoAsync(RoomID);
                    RealRoomID = (long)resp["room_info"]?["room_id"];
                }
                
                RealRoomID ??= RoomID;
            }

            //根据房间号获取弹幕服务器地址信息及验证信息
            var info = await GetDanmakuLinkInfoAsync(RealRoomID.Value);
            _url =
                $"wss://{info["host_list"]?[0]?["host"] ?? DEFAULT_DANMAKU_URL}:{info["host_list"]?[0]?["wss_port"] ?? WSS_PORT}/sub";
            _token = info["token"]?.ToString();
            _ws = new WebsocketClient(new Uri(_url), () => new ClientWebSocket { Options = { Cookies = _cookies } });

            _ws.MessageReceived.Subscribe(e => ProcessPacket(e.Binary));
            //TODO: 关闭及异常事件处理
            _ws.DisconnectionHappened.Subscribe(e =>
            {
                if (e.CloseStatus == WebSocketCloseStatus.Empty)
                    Console.WriteLine("WS CLOSED");
                else
                    Console.WriteLine("WS ERROR: " + e.Exception.Message);
                Connected = false;
            });
            await _ws.Start();
            // 如果成功连接ws 触发onopen事件，发送初始包
            if (_ws.IsStarted)
                OnOpen();
        }

        public override void Disconnect()
        {
            _ws?.Stop(WebSocketCloseStatus.Empty, string.Empty);
            _ws?.Dispose();
        }

        public override void Dispose()
        {
            Disconnect();
            _http?.Dispose();
            GC.SuppressFinalize(this);
        }

        public override void Send(byte[] packet) => _ws?.Send(packet);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override Task SendAsync(byte[] packet) => Task.Run(() => Send(packet));
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);
    }
}