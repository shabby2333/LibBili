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
        private CookieContainer _cookies = new CookieContainer();
        private HttpClient _http;
        private string _url = "wss://tx-gz-live-comet-12.chat.bilibili.com/sub";

        public WebsocketDanmakuClient(long roomID) : base(roomID) { _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies }); }
        public WebsocketDanmakuClient(long roomID, long realRoomID) : base(roomID, realRoomID){ _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies }); }

        public async override void Connect()
        {
            _ws?.Close();
            _ws?.Dispose();

            var info = await GetDanmakuLinkInfo(RealRoomID.Value);
            _url = $"wss://{info["host_list"][0]["host"]}:{info["host_list"][0]["wss_port"]}/sub";
            _token = info["token"].ToString();
            _ws = new WebSocket(_url, cookies: _cookies.GetCookies(new Uri(_url)).ToDictionary().AsEnumerable().ToList());
            //_ws.Proxy = new HttpConnectProxy(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));

            _ws.Opened += (sender, e) => OnOpen();
            _ws.DataReceived += (sender, e) => ProcessPacketAync(e.Data);
            _ws.Closed += (sender, e) => Console.WriteLine("WS CLOSED");
            _ws.Error += (sender, e) => Console.WriteLine("WS err" + e.Exception.Message);
            _ws.Open();
        }

        public override void Disconnect()
        {
            _ws?.Close();
            _ws?.Dispose();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void Send(byte[] packet) => _ws.Send(packet, 0, packet.Length);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override Task SendAsync(byte[] packet) => Task.Run(() => Send(packet));
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);

        private async Task<JToken> GetDanmakuLinkInfo(long roomID)
        {
           var resp =await _http.GetStringAsync($"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={roomID}&type=0");
           return JObject.Parse(resp)["data"];
        }

        
    }
}
