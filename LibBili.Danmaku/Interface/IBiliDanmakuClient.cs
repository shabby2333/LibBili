using LibBili.Danmaku.Model;
using LibBili.Danmaku.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace LibBili.Danmaku.Interface
{
    public abstract class IBiliDanmakuClient : IDisposable
    {
        public long? RoomID { get; }
        public long? RealRoomID { get; }
        public bool Connected { get; }
        protected Timer _timer = new Timer();
        protected string _token;

        /// <summary>
        /// 弹幕连接建立
        /// </summary>
        public event EventHandler Open;
        /// <summary>
        /// 弹幕连接关闭
        /// </summary>
        public event EventHandler Close;
        /// <summary>
        /// 弹幕连接出现异常
        /// </summary>
        public event EventHandler<Exception> Error;

        /// <summary>
        /// 准备开播
        /// </summary>
        //public event EventHandler LivePrepare;
        /// <summary>
        /// 开播
        /// </summary>
        public event EventHandler LiveStart;
        /// <summary>
        /// 下播
        /// </summary>
        //public event EventHandler LiveEnd;
        /// <summary>
        /// 直播被切
        /// </summary>
        //public event EventHandler LiveCut;

        /// <summary>
        /// 收到弹幕
        /// </summary>
        public event EventHandler<ReceiveDanmakuEventArgs> ReceiveDanmaku;
        /// <summary>
        /// 收到礼物
        /// </summary>
        public event EventHandler<ReceiveGiftEventArgs> ReceiveGift;
        /// <summary>
        /// 所有<code>Operation.ServerNotify</code>内容 包含头部原始数据
        /// </summary>
        public event EventHandler<ReceiveNoticeEventArgs> ReceiveNotice;
        /// <summary>
        /// 更新人气
        /// </summary>
        public event EventHandler<int> UpdatePopularity;
        /// <summary>
        /// 更新礼物榜
        /// </summary>
        //public event EventHandler UpdateGiftTop;
        /// <summary>
        /// 欢迎老爷
        /// </summary>
        //public event EventHandler WelcomeVip;
        /// <summary>
        /// 欢迎船员
        /// </summary>
        //public event EventHandler WelcomeGuard;

        /// <summary>
        /// 上船
        /// </summary>
        //public event EventHandler GuardBuy;
        /// <summary>
        /// 
        /// </summary>
        //public event EventHandler SuperChat;
        /// <summary>
        /// 观众互动消息
        /// </summary>
        //public event EventHandler Interact;

        public IBiliDanmakuClient(long roomID)
        {
            RoomID = roomID;
        }
        public IBiliDanmakuClient(long roomID, long realRoomID)
        {
            RealRoomID = realRoomID;
            RoomID = roomID;
        }

        public abstract void Connect();
        public abstract void Disconnect();
        public abstract void Dispose();
        public abstract void Send(byte[] packet);
        public abstract Task SendAsync(byte[] packet);
        public abstract void Send(Packet packet);
        public abstract Task SendAsync(Packet packet);

        protected virtual void OnOpen() {
            SendAsync(Packet.Authority(RealRoomID.Value, _token));
            _timer.Interval = 30 * 1000;
            _timer.Elapsed += (sender, e) => SendAsync(Packet.HeartBeat());
            _timer.Start();
        }

        protected async void ProcessPacketAync(byte[] bytes)
        {
            var packet = new Packet(bytes);
            var header = packet.Header;
            switch (header.Operation)
            {
                case Operation.AuthorityResponse:
                    Open?.Invoke(this, null);
                    //Console.WriteLine($"Authority Response:{Encoding.UTF8.GetString(bytes, 16, bytes.Length - 16) == "{\"code\":0}"}");
                    break;
                case Operation.HeartBeatResponse:
                    var popularity = BitConverter.ToInt32(packet.PacketBody.ToBigEndian(), 0);
                    UpdatePopularity?.Invoke(this, popularity);
                    break;
                case Operation.ServerNotify:
                    string body = "";
                    switch (header.ProtocolVersion)
                    {
                        case ProtocolVersion.UnCompressed:
                            body = Encoding.UTF8.GetString(packet.PacketBody, 0, header.BodyLength);
                            break;
                        case ProtocolVersion.Compressed:
                            body = Encoding.UTF8.GetString((await ZlibDeCompressAsync(packet.PacketBody)).PacketBody);
                            break;
                    }
                    var json = JObject.Parse(body);
                    ProcessNoticeAsync(body, json);
                    //Console.WriteLine(body);
                    break;
            }
        }

        protected async void ProcessNoticeAsync(string rawMessage, JObject json)
        {
            ReceiveNotice?.Invoke(this, new ReceiveNoticeEventArgs { RawMessage = rawMessage, JsonMessage = json });
            switch (json["cmd"].ToString())
            {
                case "DANMU_MSG":
                    var danmaku = new Model.Danmaku
                    {
                        UserID = json["info"][2][0].Value<int>(),
                        UserName = json["info"][2][1].ToString(),
                        Text = json["info"][1].ToString(),
                        IsAdmin = json["info"][2][2].ToString() == "1",
                        IsVIP = json["info"][2][3].ToString() == "1",
                        UserGuardLevel = json["info"][7].Value<int>()
                    };
                    ReceiveDanmaku?.Invoke(this, new ReceiveDanmakuEventArgs { Danmaku = danmaku, JsonMessage = json, RawMessage = rawMessage });
                    //Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{RoomID}]-弹幕:{(danmaku.IsAdmin?"房":"")}{(danmaku.IsVIP?"爷":"")} {danmaku.UserName + ":"}{danmaku.Text}");
                    break;
                case "SEND_GIFT":
                    var gift = new Gift
                    {
                        GiftName = json["data"]["giftName"].ToString(),
                        UserName = json["data"]["uname"].ToString(),
                        UserID = json["data"]["uid"].ToObject<int>(),
                        GiftCount = json["data"]["num"].ToObject<int>()
                    };
                    ReceiveGift?.Invoke(this, new ReceiveGiftEventArgs { Gift = gift, JsonMessage = json, RawMessage = rawMessage });
                    break;
                case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                    break;
                case "WELCOME":
                    break;
                case "ONLINERANK":
                    break;
                case "LIVE":
                    LiveStart(this, null);
                    break;
                case "PREPARING":
                    break;
                case "CUT":
                    break;
                case "SEND_TOP":
                    break;
                case "ROOM_RANK":
                    break;

            }
        }

        protected static async Task<Packet> ZlibDeCompressAsync(byte[] bytes)
        {
            //Skip Zlib Header
            using var ms = new MemoryStream(bytes, 2, bytes.Length - 2);
            using var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var headerbuffer = new byte[16];
            await zs.ReadAsync(headerbuffer, 0, 16);
            var header = new PacketHeader(headerbuffer);
            var buffer = new byte[header.BodyLength];
            await zs.ReadAsync(buffer, 0, buffer.Length);
            return new Packet { Header = header, PacketBody = buffer};
        }
    }

    /// <summary>
    /// 观众互动内容
    /// </summary>
    public enum InteractType
    {
        /// <summary>
        /// 进入
        /// </summary>
        Enter = 1,
        /// <summary>
        /// 关注
        /// </summary>
        Follow = 2,
        /// <summary>
        /// 分享直播间
        /// </summary>
        Share = 3,
        /// <summary>
        /// 特别关注
        /// </summary>
        SpecialFollow = 4,
        /// <summary>
        /// 互相关注
        /// </summary>
        MutualFollow = 5,

    }
}
