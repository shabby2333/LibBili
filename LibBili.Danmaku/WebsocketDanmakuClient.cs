using LibBili.Api.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Websocket.Client;

namespace LibBili.Danmaku
{
    public class WebsocketDanmakuClient : IBiliDanmakuClient
    {
        private WebsocketClient _ws;
        private string _url = "wss://hw-bj-live-comet-05.chat.bilibili.com/sub";

        public WebsocketDanmakuClient(long roomID) : this(roomID, null) { }
        public WebsocketDanmakuClient(long roomID, long? realRoomID) : base(roomID, realRoomID){  }

        public async override void Connect()
        {
            //尝试释放已连接的ws
            _ws?.Stop(WebSocketCloseStatus.Empty, string.Empty);
            _ws?.Dispose();

            if(!RealRoomID.HasValue){
                if (RoomID < 10000)
                {
                    var resp = await GetRoomInfoAsync(RoomID);
                    RealRoomID = (long)resp["room_info"]["room_id"];
                }
                else
                    RealRoomID = RoomID;
            }

            //根据房间号获取弹幕服务器地址信息及验证信息
            var info = await GetDanmakuLinkInfoAsync(RealRoomID.Value);
            _url = $"wss://{info["host_list"][0]["host"]}:{info["host_list"][0]["wss_port"]}/sub";
            _token = info["token"].ToString();
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
            if(_ws.IsStarted)
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
            
            // GC.SuppressFinalize(this);
            // throw new NotImplementedException();
        }

        public override void Send(byte[] packet) => _ws?.Send(packet);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override Task SendAsync(byte[] packet) => Task.Run(() => Send(packet));
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);

        

        
    }
}
