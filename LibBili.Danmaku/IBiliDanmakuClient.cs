using LibBili.Api.Util;
using LibBili.Danmaku.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibBili.Danmaku
{
    public abstract class IBiliDanmakuClient : IDisposable
    {
        public long RoomID { get; }
        public long? RealRoomID { get; protected set; }
        public bool Connected { get; protected set; }
        protected Timer _timer = null;
        protected string _token;
        protected readonly CookieContainer _cookies = new();
        protected readonly HttpClient _http;

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

        // /// <summary>
        // /// 准备开播
        // /// </summary>
        //public event EventHandler LivePrepare;
        /// <summary>
        /// 开播
        /// </summary>
        public event EventHandler LiveStart;
        // /// <summary>
        // /// 下播
        // /// </summary>
        //public event EventHandler LiveEnd;
        // /// <summary>
        // /// 直播被切
        // /// </summary>
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

        // /// <summary>
        // /// 更新礼物榜
        // /// </summary>
        //public event EventHandler UpdateGiftTop;
        /// <summary>
        /// 欢迎
        /// </summary>
        public event EventHandler Welcome;
        // /// <summary>
        // /// 欢迎老爷
        // /// </summary>
        //public event EventHandler WelcomeVip;
        // /// <summary>
        // /// 欢迎船员
        // /// </summary>
        //public event EventHandler WelcomeGuard;

        // /// <summary>
        // /// 上船
        // /// </summary>
        //public event EventHandler GuardBuy;
        // /// <summary>
        // /// 
        // /// </summary>
        //public event EventHandler SuperChat;
        // /// <summary>
        // /// 观众互动消息
        // /// </summary>
        //public event EventHandler Interact;

        public IBiliDanmakuClient(long roomID, long? realRoomID = null)
        {
            RealRoomID = realRoomID;
            RoomID = roomID;
            _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies });
        }

        public abstract void Connect();
        public abstract void Disconnect();
        public abstract void Dispose();
        public abstract void Send(byte[] packet);
        public abstract Task SendAsync(byte[] packet);
        public abstract void Send(Packet packet);
        public abstract Task SendAsync(Packet packet);

        protected virtual void OnOpen()
        {
            SendAsync(Packet.Authority(RealRoomID!.Value, _token));
            Connected = true;

            _timer?.Dispose();
            _timer = new Timer((e) => (
                (IBiliDanmakuClient)e)?.SendAsync(Packet.HeartBeat()), this, 0, 30 * 1000);
        }

        protected void ProcessPacket(ReadOnlySpan<byte> bytes) =>
            ProcessPacketAsync(new Packet(bytes));

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
                    throw new NotSupportedException(
                        "New bilibili danmaku protocol appears, please contact the author if you see this Exception.");
            }

            switch (header.Operation)
            {
                case Operation.AuthorityResponse:
                    Open?.Emit(this, null);
                    break;
                case Operation.HeartBeatResponse:
                    Array.Reverse(packet.PacketBody);
                    var popularity = BitConverter.ToInt32(packet.PacketBody);
                    UpdatePopularity?.Emit(this, popularity);
                    break;
                case Operation.ServerNotify:
                    ProcessNotice(Encoding.UTF8.GetString(packet.PacketBody));
                    break;
                // HeartBeat packet request, only send by client
                case Operation.HeartBeat:
                // This operation key only used for sending authority packet by client
                case Operation.Authority:
                default:
                    break;
            }
        }

        protected void ProcessNotice(string rawMessage)
        {
            var json = JObject.Parse(rawMessage);
            ReceiveNotice?.Emit(this, new ReceiveNoticeEventArgs { RawMessage = rawMessage, JsonMessage = json });
            switch (json["cmd"]?.ToString())
            {
                case "DANMU_MSG":
                    ReceiveDanmaku?.Emit(this, new ReceiveDanmakuEventArgs
                    {
                        Danmaku = new Danmaku
                        {
                            UserID = json["info"][2][0].Value<int>(),
                            UserName = json["info"][2][1].ToString(),
                            Text = json["info"][1].ToString(),
                            IsAdmin = json["info"][2][2].ToString() == "1",
                            IsVIP = json["info"][2][3].ToString() == "1",
                            UserGuardLevel = json["info"][7].Value<int>()
                        },
                        JsonMessage = json, RawMessage = rawMessage
                    });
                    break;
                case "SEND_GIFT":
                    ReceiveGift?.Emit(this, new ReceiveGiftEventArgs
                    {
                        Gift = new Gift
                        {
                            GiftName = json["data"]["giftName"].ToString(),
                            UserName = json["data"]["uname"].ToString(),
                            UserID = json["data"]["uid"].ToObject<int>(),
                            GiftCount = json["data"]["num"].ToObject<int>()
                        },
                        JsonMessage = json, RawMessage = rawMessage
                    });
                    break;
                case "LIVE":
                    LiveStart?.Emit(this, null);
                    break;
                case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                case "INTERACT_WORD":
                case "ONLINERANK":
                case "PREPARING":
                case "CUT":
                case "SEND_TOP":
                case "ROOM_RANK":
                    break;
            }
        }

        protected static async IAsyncEnumerable<Packet> ZlibDeCompressAsync(byte[] bytes)
        {
            // Skip Zlib Header
            // @see https://stackoverflow.com/questions/9050260/what-does-a-zlib-header-look-like
            // 78 01 - No Compression/low
            // 78 9C - Default Compression
            // 78 DA - Best Compression 
            await using var ms = new MemoryStream(bytes, 2, bytes.Length - 2);
            // await using var ms = new MemoryStream(bytes, 0, bytes.Length);
            await using var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var len = 1;
            while (len > 0)
            {
                var headerBuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerBuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerBuffer);
                var buffer = ArrayPool<byte>.Shared.Rent(header.BodyLength);
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
        }

        protected static async IAsyncEnumerable<Packet> BrotliDecompressAsync(byte[] bytes)
        {
            await using var ms = new MemoryStream(bytes, 0, bytes.Length);
            await using var zs = new BrotliStream(ms, CompressionMode.Decompress);
            var len = 1;
            while (len > 0)
            {
                var headerBuffer = new byte[PacketHeader.PACKET_HEADER_LENGTH];
                len = await zs.ReadAsync(headerBuffer.AsMemory(0, PacketHeader.PACKET_HEADER_LENGTH));
                if (len == 0) break;
                var header = new PacketHeader(headerBuffer);
                var buffer = ArrayPool<byte>.Shared.Rent(header.BodyLength);
                len = await zs.ReadAsync(buffer.AsMemory(0, buffer.Length));
                yield return new Packet { Header = header, PacketBody = buffer };
            }
        }

        protected async Task<JToken> GetDanmakuLinkInfoAsync(long roomID)
        {
            var resp = await _http.GetStringAsync(
                $"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={roomID}&type=0");
            return JObject.Parse(resp)["data"];
        }

        protected async Task<JToken> GetRoomInfoAsync(long roomID)
        {
            var resp = await _http.GetStringAsync(
                $"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id={roomID}");
            return JObject.Parse(resp)["data"];
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