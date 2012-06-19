using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FitBit.NET
{
    internal class ANT
    {
        internal static byte _channel = 0x00;

        internal static byte[] Reset()
        {
            return ANTWrapped(new byte[] { 0x4a, 0x00 });
        }

        internal static byte[] SetChannelFrequency(byte frequency)
        {
            return ANTWrapped(new byte[] { 0x45, _channel, frequency });
        }

        internal static byte[] SetTransmitPower(byte power)
        {
            return ANTWrapped(new byte[] { 0x47, 0x0, power });
        }

        internal static byte[] SetSearchTimeout(byte timeout)
        {
            return ANTWrapped(new byte[] { 0x44, _channel, timeout });
        }

        internal static byte[] SendNetworkKey(byte network, byte[] key)
        {
            var wrapped = new byte[2 + key.Length];
            wrapped[0] = 0x46;
            wrapped[1] = network;
            key.CopyTo(wrapped, 2);
            return ANTWrapped(wrapped);
        }

        internal static byte[] SetChannelPeriod(byte[] period)
        {
            var wrapped = new byte[2 + period.Length];
            wrapped[0] = 0x43;
            wrapped[1] = _channel;
            period.CopyTo(wrapped, 2);
            return ANTWrapped(wrapped);
        }

        internal static byte[] SetChannelId(byte[] id)
        {
            var wrapped = new byte[2 + id.Length];
            wrapped[0] = 0x51;
            wrapped[1] = _channel;
            id.CopyTo(wrapped, 2);
            return ANTWrapped(wrapped);
        }

        internal static byte[] Reset4F()
        {
            // Wrong in docs, right as 0x78 0x01, 6-19-12
            return ANTWrapped(new byte[] { 0x4F, _channel, 0x78, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        }

        internal static byte[] ResetTracker()
        {
            // return ANTWrapped(new byte[] { 0x78, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            return ANTWrapped(new byte[] { 0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        }

        internal static byte[] DeviceIDReset4F(byte[] channel)
        {
            return ANTWrapped(new byte[] { 0x4F, _channel, 0x78, 0x02, channel[0], channel[1], 0x00, 0x00, 0x00, 0x00 });
        }

        internal static byte[] DeviceIDReset(byte[] channel)
        {
            return ANTWrapped(new byte[] { 0x78, 0x02, channel[0], channel[1], 0x00, 0x00, 0x00, 0x00 });
        }

        internal static byte[] Ping()
        {
            return ANTWrapped(new byte[] { 0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        }

        internal static byte[] OpenChannel()
        {
            return ANTWrapped(new byte[] { 0x4b, _channel });
        }

        internal static byte[] CloseChannel()
        {
            return ANTWrapped(new byte[] { 0x4c, _channel });
        }

        internal static byte[] AssignChannel()
        {
            return ANTWrapped(new byte[] { 0x42, _channel, 0x00, 0x00 });
        }

        internal static byte[] Data4F(byte[] data)
        {
            var wrapped = new byte[2 + data.Length];
            wrapped[0] = 0x4f;
            wrapped[1] = _channel;
            data.CopyTo(wrapped, 2);
            return ANTWrapped(wrapped);
        }

        internal static byte[] ANTWrapped(byte[] packet)
        {
            if (packet.Length > 10)
                throw new PacketTooLongException("The maximum ANT packet length is 9 bytes.");

            var opcode = packet[0];
            var data = new byte[packet.Length - 1];
            for (int i = 1; i < packet.Length; i++)
                data[i-1] = packet[i];

            var finalPacket = new byte[packet.Length + 3];
            finalPacket[0] = 0xA4;
            finalPacket[1] = (byte)data.Length;
            finalPacket[2] = opcode;
            data.CopyTo(finalPacket, 3);
            finalPacket[3 + data.Length] = (byte)Utility.XOR(finalPacket);
            return finalPacket;
        }
    }
}
