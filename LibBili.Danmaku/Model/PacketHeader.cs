﻿using LibBili.Danmaku.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;



namespace LibBili.Danmaku.Model
{
    /// <summary>
    /// 弹幕数据包头部
    /// </summary>
    public class PacketHeader : IEquatable<PacketHeader>
    {
        public const int PACKET_HEADER_LENGTH = 16;
        public const int PACKET_LENGTH_OFFSET = 0;
        public const int PACKET_LENGTH_LENGTH = 4;
        public const int HEADER_LENFTH_OFFSET = 4;
        public const int HEADER_LENGTH_LENGTH = 2;
        public const int PROTOCOL_VERSION_OFFSET = 6;
        public const int PROTOCOL_VERSION_LENGTH = 2;
        public const int OPERATION_OFFSET = 8;
        public const int OPERATION_LENGTH = 4;
        public const int SEQUENCE_ID_OFFSET = 12;
        public const int SEQUENCE_ID_LENGTH = 4;


        public int PacketLength;
        public short HeaderLength = PACKET_HEADER_LENGTH;
        public ProtocolVersion ProtocolVersion;
        public Operation Operation;
        public int SequenceId = 1;
        public int BodyLength { get => PacketLength - HeaderLength; }

        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="bytes">弹幕头16字节</param>
        public PacketHeader(byte[] bytes)
        {
            if (bytes.Length < PACKET_HEADER_LENGTH) throw new ArgumentException("No Supported Protocol Header");
            PacketLength = BitConverter.ToInt32(bytes[PACKET_LENGTH_OFFSET..(PACKET_LENGTH_OFFSET + PACKET_LENGTH_LENGTH)].ToBigEndian());
            HeaderLength = BitConverter.ToInt16(bytes[4..6].ToBigEndian());
            ProtocolVersion = (ProtocolVersion)BitConverter.ToInt16(bytes[6..8].ToBigEndian());
            Operation = (Operation)BitConverter.ToInt32(bytes[8..12].ToBigEndian());
            SequenceId = BitConverter.ToInt32(bytes[12..PACKET_HEADER_LENGTH].ToBigEndian());
        }

        public PacketHeader() { }

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
            Array.Copy(BitConverter.GetBytes(PacketLength).ToBigEndian(), 0, bytes, 0, 4);
            Array.Copy(BitConverter.GetBytes(HeaderLength).ToBigEndian(), 0, bytes, 4, 2);
            Array.Copy(BitConverter.GetBytes((short)ProtocolVersion).ToBigEndian(), 0, bytes, 6, 2);
            Array.Copy(BitConverter.GetBytes((int)Operation).ToBigEndian(), 0, bytes, 8, 4);
            Array.Copy(BitConverter.GetBytes(SequenceId).ToBigEndian(), 0, bytes, 12, 4);
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
        Brotli = 3
    }
}
