using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LibBili.Danmaku
{
    public class TcpDanmakuClient : IBiliDanmakuClient
    {
        private const int TCP_PORT = 2243;

        private TcpClient _socket; 
        private string _url = "hw-bj-live-comet-05.chat.bilibili.com";
        private int _port = TCP_PORT;

        public TcpDanmakuClient(long roomID) : this(roomID, null) { }
        public TcpDanmakuClient(long roomID, long? realRoomID) : base(roomID, realRoomID) {  }


        public async override void Connect()
        {
            if(_socket is not null)
            {
                _socket?.Close();
                _socket.Dispose();
                _socket = null;
            }

            if (!RealRoomID.HasValue)
            {
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
            _url = info["host_list"][0]["host"].ToString();
            _port = info["host_list"][0]["port"].ToObject<int>();
            _token = info["token"].ToString();

            _socket = new TcpClient();
            _socket.Connect(_url, _port);
            if(_socket.Connected)
                OnOpen();

            while(_socket is not null && _socket.Connected)
            {
                var bytes = ArrayPool<byte>.Shared.Rent(_socket.ReceiveBufferSize);
                var ms = new MemoryStream();
                int len;
                do
                {
                    len = await _socket.GetStream().ReadAsync(bytes);
                    ms.Write(bytes);
                } while (len >= bytes.Length);
                ArrayPool<byte>.Shared.Return(bytes);

                ProcessPacket(ms.ToArray().AsSpan());
            }
            
        }

        public override void Disconnect()
        {
            _socket?.Close();
            Connected = false;
        }

        public override void Dispose()
        {
            Disconnect();
            _socket?.Dispose();
        }

        public override void Send(byte[] packet) => _socket?.GetStream()?.Write(packet);
        public override void Send(Packet packet) => Send(packet.ToBytes);
        public override async Task SendAsync(byte[] packet) => await _socket.GetStream().WriteAsync(packet);
        public override Task SendAsync(Packet packet) => SendAsync(packet.ToBytes);

    }
}
