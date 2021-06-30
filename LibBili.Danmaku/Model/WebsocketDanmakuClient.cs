using LibBili.Api.Util;
using LibBili.Danmaku.Util;
using Newtonsoft.Json.Linq;
using SuperSocket.ClientEngine.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WebSocket4Net;

namespace LibBili.Danmaku.Model
{
    public class WebsocketDanmakuClient : Interface.IBiliDanmakuClient
    {
        private WebSocket _ws;
        private CookieContainer _cookies = new();
        private HttpClient _http;
        private string _url = "wss://hw-bj-live-comet-05.chat.bilibili.com/sub";

        public WebsocketDanmakuClient(long roomID) : this(roomID, null) { }
        public WebsocketDanmakuClient(long roomID, long? realRoomID) : base(roomID, realRoomID){ _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies }); }

        public async override void Connect()
        {
            //尝试释放已连接的ws
            _ws?.Close();
            _ws?.Dispose();

            if(!RealRoomID.HasValue){
                var resp = await GetRoomInfoAsync(RoomID);
                RealRoomID = (long)resp["room_info"]["room_id"];
            }

            //System.Console.WriteLine("realroomId: " + RealRoomID);
            //根据房间号获取弹幕服务器地址信息及验证信息
            var info = await GetDanmakuLinkInfoAsync(RealRoomID.Value);
            _url = $"wss://{info["host_list"][0]["host"]}:{info["host_list"][0]["wss_port"]}/sub";
            _token = info["token"].ToString();
            _ws = new WebSocket(_url, cookies: _cookies.GetCookies(new Uri(_url)).ToDictionary().AsEnumerable().ToList());
            //_ws.Proxy = new HttpConnectProxy(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));

            _ws.Opened += (sender, e) => OnOpen();
            _ws.DataReceived += (sender, e) => ProcessPacket(e.Data);
            //TODO: 关闭及异常事件处理
            _ws.Closed += (sender, e) => { Console.WriteLine("WS CLOSED"); Connected = false; };
            _ws.Error += (sender, e) => {Console.WriteLine("WS err" + e.Exception.Message); Connected = false;};
        _ws.Open();
        }

        public override void Disconnect()
        {
            _ws?.Close();
            _ws?.Dispose();
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            //throw new NotImplementedException();
        }

        public override void Send(byte[] packet) => _ws.Send(packet, 0, packet.Length);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override Task SendAsync(byte[] packet) => Task.Run(() => Send(packet));
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);

        private async Task<JToken> GetDanmakuLinkInfoAsync(long roomID)
        {
           var resp =await _http.GetStringAsync($"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={roomID}&type=0");
           return JObject.Parse(resp)["data"];
        }

        private async Task<JToken> GetRoomInfoAsync(long roomID)
        {
           var resp =await _http.GetStringAsync($"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id={roomID}");
           return JObject.Parse(resp)["data"];
        }

        
    }
}
