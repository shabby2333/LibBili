using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibBili.Danmaku.Model
{
    public class Packet
    {
        private static readonly Packet _NoBodyHeartBeatPacket = new()
        {
            Header = new PacketHeader()
            {
                PacketLength = PacketHeader.PACKET_HEADER_LENGTH,
                ProtocolVersion = ProtocolVersion.HeartBeat,
                Operation = Operation.HeartBeat
            }
        }; 

        public PacketHeader Header { get; set; }
        public int Length { get => Header.PacketLength; }
        public byte[] PacketBody { get; set; }

        public Packet() { }

        public Packet(ref byte[] bytes)
        {
            var headerBuffer = bytes[0..PacketHeader.PACKET_HEADER_LENGTH];
            Header = new PacketHeader(headerBuffer);
            PacketBody = bytes[Header.HeaderLength..Header.PacketLength];
        }

        public Packet(Operation operation,byte[] body = null)
        {
            Header = new PacketHeader
            {
                Operation = operation,
                ProtocolVersion = ProtocolVersion.UnCompressed,
                PacketLength = PacketHeader.PACKET_HEADER_LENGTH + (body == null? 0: body.Length)
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
                Array.Copy(Header.ToBytes, arr, Header.HeaderLength);
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
                    Operation = Operation.HeartBeat
                },
                PacketBody = msg
            };
        }

        /// <summary>
        /// 生成验证用数据包
        /// </summary>
        /// <param name="roomID"></param>
        /// <param name="token"></param>
        /// <param name="uid"></param>
        /// <returns></returns>
        public static Packet Authority(long roomID, string token,int uid = 0, ProtocolVersion protocolVersion = ProtocolVersion.Brotli)
        {
            var obj = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                new { 
                    roomid = roomID, 
                    uid = uid, 
                    protover = (int)protocolVersion, 
                    key = token, 
                    platform = "web", 
                    clientver="2.1.7", 
                    type = 2  }));
            return new Packet
            {
                Header = new PacketHeader
                {
                    Operation = Operation.Authority,
                    ProtocolVersion = ProtocolVersion.HeartBeat,
                    PacketLength = PacketHeader.PACKET_HEADER_LENGTH + obj.Length
                },
                PacketBody = obj
            };
        }
    }

    
}
