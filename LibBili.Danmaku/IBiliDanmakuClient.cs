using LibBili.Api.Util;
using LibBili.Danmaku.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibBili.Danmaku
{
    public abstract class IBiliDanmakuClient : IDisposable
    {
        public long RoomID { get; }
        public long? RealRoomID { get; protected set;}
        public bool Connected { get; protected set; }
        protected Timer _timer = null;
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
        /// 欢迎
        /// </summary>
        public event EventHandler Welcome;
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
        public IBiliDanmakuClient(long roomID, long? realRoomID)
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
            Connected = true;

            if(_timer != null)
                _timer.Dispose();
            _timer = new Timer((e) => (
            (IBiliDanmakuClient)e)?.SendAsync(Packet.HeartBeat())
            , this, 0, 30 * 1000);
            
        }



        protected void ProcessPacket(byte[] bytes) =>
            ProcessPacketAsync(new Packet(ref bytes));

        protected async void ProcessPacketAsync(Packet packet)
        {
            var header = packet.Header;
            switch (header.ProtocolVersion)
            {
                case ProtocolVersion.UnCompressed:
                case ProtocolVersion.HeartBeat:
                    break;
                case ProtocolVersion.Zlib:
                    await foreach (var packet1 in ZlibDeCompressAsync(packet.PacketBody))
                        ProcessPacketAsync(packet1);
                    return;
                case ProtocolVersion.Brotli:
                    await foreach (var packet1 in BrotliDecompressAsync(packet.PacketBody))
                        ProcessPacketAsync(packet1);
                    return;
                default:
                    throw new Exception();
            }
            switch (header.Operation)
            {
                case Operation.AuthorityResponse:
                    Open?.Emit(this, null);
                    //Console.WriteLine($"Authority Response:{Encoding.UTF8.GetString(bytes, 16, bytes.Length - 16) == "{\"code\":0}"}");
                    break;
                case Operation.HeartBeatResponse:
                    Array.Reverse(packet.PacketBody);
                    var popularity = BitConverter.ToInt32(packet.PacketBody);
                    UpdatePopularity?.Emit(this, popularity);
                    break;
                case Operation.ServerNotify:
                    ProcessNotice(Encoding.UTF8.GetString(packet.PacketBody));
                    break;

            }
        }

        protected void ProcessNotice(string rawMessage)
        {
            var json = JObject.Parse(rawMessage);
            ReceiveNotice?.Emit(this, new ReceiveNoticeEventArgs { RawMessage = rawMessage, JsonMessage = json });
            //Debug.WriteLine(rawMessage);
            switch (json["cmd"].ToString())
            {
                case "DANMU_MSG":
                    var danmaku = new Danmaku
                    {
                        UserID = json["info"][2][0].Value<int>(),
                        UserName = json["info"][2][1].ToString(),
                        Text = json["info"][1].ToString(),
                        IsAdmin = json["info"][2][2].ToString() == "1",
                        IsVIP = json["info"][2][3].ToString() == "1",
                        UserGuardLevel = json["info"][7].Value<int>()
                    };
                    ReceiveDanmaku?.Emit(this, new ReceiveDanmakuEventArgs { Danmaku = danmaku, JsonMessage = json, RawMessage = rawMessage });
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
                    ReceiveGift?.Emit(this, new ReceiveGiftEventArgs { Gift = gift, JsonMessage = json, RawMessage = rawMessage });
                    break;
                case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                    break;
                case "INTERACT_WORD":
                    //Console.WriteLine(rawMessage);

                    break;
                case "ONLINERANK":
                    break;
                case "LIVE":
                    LiveStart?.Emit(this, null);
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

        protected static async IAsyncEnumerable<Packet> ZlibDeCompressAsync(byte[] bytes)
        {
            //Skip Zlib Header
            using var ms = new MemoryStream(bytes, 2, bytes.Length - 2);
            using var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var len = 1;
            while(len > 0)
            {
                var headerbuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerbuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerbuffer);
                var buffer = new byte[header.BodyLength];
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
        }

        protected static async IAsyncEnumerable<Packet> BrotliDecompressAsync(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes, 0, bytes.Length);
            using var zs = new BrotliStream(ms, CompressionMode.Decompress);
            var len = 1;
            while (len > 0)
            {
                var headerbuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerbuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerbuffer);
                var buffer = new byte[header.BodyLength];
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
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
