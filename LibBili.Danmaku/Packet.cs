using System;
using System.Text;
using System.Text.Json;

namespace LibBili.Danmaku
{
    public struct Packet
    {
        private static readonly Packet _NoBodyHeartBeatPacket = new()
        {
            Header = new PacketHeader()
            {
                HeaderLength = PacketHeader.PACKET_HEADER_LENGTH,
                SequenceId = 1,
                ProtocolVersion = ProtocolVersion.HeartBeat,
                Operation = Operation.HeartBeat
            }
        };

        public PacketHeader Header;

        public int Length
        {
            get => Header.PacketLength;
        }

        public byte[] PacketBody;

        public Packet(ReadOnlySpan<byte> bytes)
        {
            var headerBuffer = bytes[0..PacketHeader.PACKET_HEADER_LENGTH];
            Header = new PacketHeader(headerBuffer);
            PacketBody = bytes[Header.HeaderLength..Header.PacketLength].ToArray();
        }

        public Packet(Operation operation, byte[] body = null)
        {
            Header = new PacketHeader
            {
                Operation = operation,
                ProtocolVersion = ProtocolVersion.UnCompressed,
                PacketLength = PacketHeader.PACKET_HEADER_LENGTH + (body == null ? 0 : body.Length)
            };
            PacketBody = body;
        }

        public byte[] ToBytes
        {
            get
            {
                if (PacketBody != null)
                    Header.PacketLength = Header.HeaderLength + PacketBody.Length;
                else
                    Header.PacketLength = Header.HeaderLength;
                var arr = new byte[Header.PacketLength];
                Array.Copy(((ReadOnlySpan<byte>)Header).ToArray(), arr, Header.HeaderLength);
                if (PacketBody != null)
                    Array.Copy(PacketBody, 0, arr, Header.HeaderLength, PacketBody.Length);
                return arr;
            }
        }

        /// <summary>
        /// 生成附带msg信息的心跳包
        /// </summary>
        /// <param name="msg">需要带的信息</param>
        /// <returns>心跳包</returns>
        public static Packet HeartBeat(string msg)
        {
            return HeartBeat(Encoding.UTF8.GetBytes(msg));
        }

        /// <summary>
        /// 生成附带msg信息的心跳包
        /// </summary>
        /// <param name="msg">需要带的信息</param>
        /// <returns>心跳包</returns>
        public static Packet HeartBeat(byte[] msg = null)
        {
            if (msg == null) return _NoBodyHeartBeatPacket;
            return new Packet()
            {
                Header = new PacketHeader()
                {
                    PacketLength = PacketHeader.PACKET_HEADER_LENGTH + msg.Length,
                    ProtocolVersion = ProtocolVersion.HeartBeat,
                    Operation = Operation.HeartBeat,
                    SequenceId = 1,
                    HeaderLength = PacketHeader.PACKET_HEADER_LENGTH
                },
                PacketBody = msg
            };
        }

        public static readonly JsonSerializerOptions serializeOptions = new() {
            IncludeFields = true,
        };

    /// <summary>
    /// 生成验证用数据包
    /// </summary>
    /// <param name="roomID">房间号</param>
    /// <param name="token">http请求获取的token</param>
    /// <param name="uid">个人UID</param>
    /// <param name="protocolVersion">协议版本</param>
    /// <returns>验证请求数据包</returns>
    public static Packet Authority(long roomID, string token, int uid = 0,
            ProtocolVersion protocolVersion = ProtocolVersion.Brotli)
        {
            var obj = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new AuthorityBody {
                roomid = roomID,
                uid = uid,
                protover = (int)protocolVersion,
                key = token,
                platform = "web",
                // 2021.10.16 抓包发现目前不传输clientver信息
                //clientver="2.1.7", 
                type = 2
            }, serializeOptions);
            return new Packet
            {
                Header = new PacketHeader
                {
                    Operation = Operation.Authority,
                    ProtocolVersion = ProtocolVersion.HeartBeat,
                    SequenceId = 1,
                    HeaderLength = PacketHeader.PACKET_HEADER_LENGTH,
                    PacketLength = PacketHeader.PACKET_HEADER_LENGTH + obj.Length
                },
                PacketBody = obj
            };
        }
    }

    public struct AuthorityBody {
        public long roomid;
        public long uid;
        public int protover;
        public string key;
        public string platform;
        public int type;
    }
}