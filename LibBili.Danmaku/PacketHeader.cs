using System;
using System.Buffers.Binary;
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
    public struct PacketHeader
    {
        public const int PACKET_HEADER_LENGTH = 16;

        public int PacketLength;
        public short HeaderLength;
        public ProtocolVersion ProtocolVersion;
        public Operation Operation;
        public int SequenceId;

        public int BodyLength
        {
            get => PacketLength - HeaderLength;
        }

        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="bytes">弹幕头16字节</param>
        public PacketHeader(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < PACKET_HEADER_LENGTH) throw new ArgumentException("No Supported Protocol Header");

            var b = bytes;
            PacketLength = BinaryPrimitives.ReadInt32BigEndian(b[0..4]);
            HeaderLength = BinaryPrimitives.ReadInt16BigEndian(b[4..6]);
            ProtocolVersion = (ProtocolVersion)BinaryPrimitives.ReadInt16BigEndian(b[6..8]);
            Operation = (Operation)BinaryPrimitives.ReadInt32BigEndian(b[8..12]);
            SequenceId = BinaryPrimitives.ReadInt32BigEndian(b[12..16]);
        }

        /// <summary>
        /// 生成弹幕协议的头部
        /// </summary>
        /// <returns>所对应的弹幕头部byte数组</returns>
        public static explicit operator ReadOnlySpan<byte>(PacketHeader header) => GetBytes(header.PacketLength,
            header.HeaderLength, header.ProtocolVersion, header.Operation, header.SequenceId);

        /// <summary>
        /// 生成弹幕协议的头部
        /// </summary>
        /// <param name="packetLength">消息数据包长度</param>
        /// <param name="headerLength">头部长度</param>
        /// <param name="protocolVersion">弹幕协议版本</param>
        /// <param name="operation">数据包操作</param>
        /// <param name="sequenceId">序列号</param>
        /// <returns></returns>
        public static byte[] GetBytes(int packetLength, short headerLength, ProtocolVersion protocolVersion,
            Operation operation, int sequenceId = 1)
        {
            var bytes = new byte[PACKET_HEADER_LENGTH].AsSpan();
            BinaryPrimitives.WriteInt32BigEndian(bytes[0..4], packetLength);
            BinaryPrimitives.WriteInt16BigEndian(bytes[4..6], headerLength);
            BinaryPrimitives.WriteInt16BigEndian(bytes[6..8], (short)protocolVersion);
            BinaryPrimitives.WriteInt32BigEndian(bytes[8..12], (int)operation);
            BinaryPrimitives.WriteInt32BigEndian(bytes[12..16], sequenceId);

            return bytes.ToArray();
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