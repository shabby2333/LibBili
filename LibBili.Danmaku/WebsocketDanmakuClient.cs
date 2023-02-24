using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace LibBili.Danmaku
{
    public class WebsocketDanmakuClient : BiliDanmakuClient
    {
        private const int WS_PORT = 2244;
        private const int WSS_PORT = 443;
        private const string DEFAULT_DANMAKU_URL = "hw-bj-live-comet-05.chat.bilibili.com";


        private ClientWebSocket _ws;
        private string _url = "wss://hw-bj-live-comet-05.chat.bilibili.com/sub";
        private bool _close = true;

        public WebsocketDanmakuClient(long roomID, long? realRoomID = null) : base(roomID, realRoomID)
        {
        }

        public override async void Connect()
        {
            //尝试释放已连接的ws
            _ws?.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);
            _ws?.Dispose();

            if (!RealRoomID.HasValue)
            {
                // 汪姐姐说短房间号只有10000以下的，出问题记得点艹他
                if (RoomID < 10000)
                {
                    var resp = await GetRoomInfoAsync(RoomID);
                    RealRoomID = (long)resp["room_info"]?["room_id"];
                }
                else
                    RealRoomID = RoomID;
            }

            //根据房间号获取弹幕服务器地址信息及验证信息
            var info = await GetDanmakuLinkInfoAsync(RealRoomID.Value);
            _url =
                $"wss://{info["host_list"]?[0]?["host"] ?? DEFAULT_DANMAKU_URL}:{info["host_list"]?[0]?["wss_port"] ?? WSS_PORT}/sub";
            _token = info["token"]?.ToString();
            _ws = new ClientWebSocket { Options = { Cookies = _cookies } };
            await _ws.ConnectAsync(new Uri(_url), CancellationToken.None);
            _close = false;

            // 如果成功连接ws 触发onopen事件，发送初始包
            if (_ws.State == WebSocketState.Open)
                OnOpen();

            var mem = new byte[4096];
            _ = Task.Run(async () =>
            {
                while (!_close) {
                    if (_ws.State != WebSocketState.Open) {
                        Console.WriteLine($"{RoomID}-连接已中断，三秒后自动重连");
                        await Task.Delay(3000);
                        Connect();
                    }

                    var ms = new MemoryStream();
                    //var mem = ArrayPool<byte>.Shared.Rent(4096);
                    while (true) {
                        var res = await _ws.ReceiveAsync(mem, CancellationToken.None);
                        await ms.WriteAsync(mem.AsMemory(0, res.Count));
                        if (res.EndOfMessage) break;
                    }
                    ProcessPacket(ms.ToArray());
                }
            });

        }

        public override async void Disconnect()
        {
            await _ws?.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);
            _ws?.Dispose();
        }

        public override void Dispose()
        {
            Disconnect();
            _http?.Dispose();
            GC.SuppressFinalize(this);
        }

        public override void Send(byte[] packet) => _ws?.SendAsync(packet, WebSocketMessageType.Binary, true, CancellationToken.None);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override Task SendAsync(byte[] packet) => Task.Run(() => Send(packet));
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);
    }
}