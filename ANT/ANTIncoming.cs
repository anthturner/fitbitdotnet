using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Diagnostics;

namespace FitBit.NET.ANT
{
    public static class ANTIncoming
    {
        public static void Parse(byte[] msg)
        {
            if (msg[0] != 0xA4) // NOT ANT
                return;
            var antParsed = new ANTIncomingMessage(msg);
            switch (msg[2])
            {
                case 0x6F:
                    STARTUP_MESSAGE(ref antParsed);
                    break;
                case 0x40:
                    CHANNEL_RESPONSE(ref antParsed);
                    break;
            }

            Debug.Write(antParsed);
            Console.Write(antParsed);
        }

        static void STARTUP_MESSAGE(ref ANTIncomingMessage msg)
        {
            msg.Type = "STARTUP_MESSAGE";
            StringBuilder status = new StringBuilder();
            var statcode = msg.Buffer[3];

            if (statcode == 0x00)
                status.Append("POWER_ON_RESET ");

            BitArray msgBits = new BitArray(new byte[] { statcode });

            if (msgBits[0])
                status.Append("HARDWARE_RESET_LINE ");
            if (msgBits[1])
                status.Append("WATCH_DOG_RESET ");
            if (msgBits[5])
                status.Append("COMMAND_RESET ");
            if (msgBits[6])
                status.Append("SYNCHRONOUS_RESET ");
            if (msgBits[7])
                status.Append("SUSPEND_RESET ");

            msg.ParsedContent = status.ToString();
        }

        static void CHANNEL_RESPONSE(ref ANTIncomingMessage msg)
        {
            msg.Type = "CHANNEL_RESPONSE";
            StringBuilder status = new StringBuilder();
            var chanNum = msg.Buffer[3];
            var messageId = msg.Buffer[4];
            var messageCode = msg.Buffer[5];

            status.AppendLine("Channel Number: " + chanNum + " [" + Utility.StringValueOfByte(chanNum) + "]");
            status.AppendLine("Message ID: " + Utility.StringValueOfByte(messageId) + (messageId == 1 ? " ... !RF EVENT!" : ""));
            status.Append("Message Code: [" + messageCode + "]-> ");

            // TODO: Flesh this out
            Dictionary<int, string> codes = new Dictionary<int,string>();
            codes.Add(0, "RESPONSE_NO_ERROR");
            codes.Add(1, "EVENT_RX_SEARCH_TIMEOUT");
            codes.Add(2, "EVENT_RX_FAIL");
            codes.Add(3, "EVENT_TX");
            codes.Add(4, "EVENT_TRANSFER_RX_FAILED");
            codes.Add(5, "EVENT_TRANSFER_TX_COMPLETED");
            codes.Add(6, "EVENT_TRANSFER_TX_FAILED");
            codes.Add(7, "EVENT_CHANNEL_CLOSED");

            if (codes.ContainsKey(messageCode))
                status.AppendLine(codes[messageCode]);
            else
                status.AppendLine("UNKNOWN");

            msg.ParsedContent = status.ToString();
        }
    }

    internal class ANTIncomingMessage
    {
        public byte TypeByte;
        public string Type;
        public int Size;
        public string ParsedContent;
        public bool ChecksumValid = false;
        public byte[] Buffer;

        public ANTIncomingMessage(byte[] buffer)
        {
            Buffer = buffer;
            Size = buffer[1];
            TypeByte = buffer[2];
            var targetCheckSum = Utility.XOR(Utility.Trim(buffer, 0, buffer.Length - 1));
            if (buffer[buffer.Length - 1] == targetCheckSum)
                ChecksumValid = true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[" + Utility.StringValueOfByte(TypeByte).PadLeft(2));
            if (Type != null)
                sb.Append("/" + Type.PadLeft(15) + "] ... ");
            else
                sb.Append("] ... ");
            sb.AppendLine(Size + "b ... Checksum Valid? " + (ChecksumValid ? "YES" : "NO"));
            if (ParsedContent != null)
                sb.AppendLine(ParsedContent);
            sb.AppendLine("Buffer> [ " + Utility.StringValueOfByte(Buffer, ' ') + " ]");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
