using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;



namespace LibBili.Danmaku
{
    /// <summary>
    /// 弹幕数据包头部
    /// </summary>
    public struct PacketHeader : IEquatable<PacketHeader>
    {
        public const int PACKET_HEADER_LENGTH = 16;


        public int PacketLength;
        public short HeaderLength;
        public ProtocolVersion ProtocolVersion;
        public Operation Operation;
        public int SequenceId;
        public int BodyLength { get => PacketLength - HeaderLength; }

        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="bytes">弹幕头16字节</param>
        public PacketHeader(byte[] bytes)
        {
            if (bytes.Length < PACKET_HEADER_LENGTH) throw new ArgumentException("No Supported Protocol Header");

            Array.Reverse(bytes, 0, 4);
            Array.Reverse(bytes, 4, 2);
            Array.Reverse(bytes, 6, 2);
            Array.Reverse(bytes, 8, 4);
            Array.Reverse(bytes, 12, 4);
            var b = bytes.AsSpan();
            PacketLength = BitConverter.ToInt32(b[0..4]);
            HeaderLength = BitConverter.ToInt16(b[4..6]);
            ProtocolVersion = (ProtocolVersion)BitConverter.ToInt16(b[6..8]);
            Operation = (Operation)BitConverter.ToInt32(b[8..12]);
            SequenceId = BitConverter.ToInt32(b[12..16]);
        }


        /// <summary>
        /// 生成弹幕协议的头部
        /// </summary>
        /// <returns>所对应的弹幕头部byte数组</returns>
        public byte[] ToBytes => GetBytes(PacketLength, HeaderLength, ProtocolVersion, Operation, SequenceId);

        public override bool Equals(object obj)
        {
            return obj is PacketHeader header && Equals(header);
        }

        public bool Equals(PacketHeader other)
        {
            return EqualityComparer<byte[]>.Default.Equals(ToBytes, other.ToBytes);
        }

        public override int GetHashCode()
        {
            return -810706433 + EqualityComparer<byte[]>.Default.GetHashCode(ToBytes);
        }

        public static bool operator ==(PacketHeader left, PacketHeader right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PacketHeader left, PacketHeader right)
        {
            return !(left == right);
        }

        /// <summary>
        /// 生成弹幕协议的头部
        /// </summary>
        /// <param name="PacketLength">消息数据包长度</param>
        /// <param name="HeaderLength">头部长度</param>
        /// <param name="ProtocolVersion">弹幕协议版本</param>
        /// <param name="Operation">数据包操作</param>
        /// <param name="SequenceId">序列号</param>
        /// <returns></returns>
        public static byte[] GetBytes(int PacketLength, short HeaderLength, ProtocolVersion ProtocolVersion, Operation Operation, int SequenceId = 1)
        {
            var bytes = new byte[PACKET_HEADER_LENGTH];
            Array.Copy(BitConverter.GetBytes(PacketLength), 0, bytes, 0, 4);
            Array.Copy(BitConverter.GetBytes(HeaderLength), 0, bytes, 4, 2);
            Array.Copy(BitConverter.GetBytes((short)ProtocolVersion), 0, bytes, 6, 2);
            Array.Copy(BitConverter.GetBytes((int)Operation), 0, bytes, 8, 4);
            Array.Copy(BitConverter.GetBytes(SequenceId), 0, bytes, 12, 4);

            Array.Reverse(bytes, 0, 4);
            Array.Reverse(bytes, 4, 2);
            Array.Reverse(bytes, 6, 2);
            Array.Reverse(bytes, 8, 4);
            Array.Reverse(bytes, 12, 4);
            
            return bytes;
        }
    }

    /// <summary>
    /// 操作数据
    /// </summary>
    public enum Operation
    {
        /// <summary>
        /// 心跳包
        /// </summary>
        HeartBeat = 2,
        /// <summary>
        /// 服务器心跳回应(包含人气信息)
        /// </summary>
        HeartBeatResponse = 3,
        /// <summary>
        /// 服务器消息(正常消息)
        /// </summary>
        ServerNotify = 5,
        /// <summary>
        /// 客户端认证请求
        /// </summary>
        Authority = 7,
        /// <summary>
        /// 认证回应
        /// </summary>
        AuthorityResponse = 8
    }

    /// <summary>
    /// 弹幕协议版本
    /// </summary>
    public enum ProtocolVersion
    {
        /// <summary>
        /// 未压缩数据
        /// </summary>
        UnCompressed = 0,
        /// <summary>
        /// 心跳数据
        /// </summary>
        HeartBeat = 1,
        /// <summary>
        /// zlib数据
        /// </summary>
        Zlib = 2,
        /// <summary>
        /// Br数据
        /// </summary>
        Brotli = 3
    }
}
