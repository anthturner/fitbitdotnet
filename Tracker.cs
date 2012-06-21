using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FitBit.NET.ANT;

namespace FitBit.NET
{
    public class Tracker
    {
        public byte[] SerialNumber;
        public byte FirmwareVersion;
        public Version BSLVersion;
        public Version AppVersion;
        public byte InBSLMode;
        public bool DeviceOnCharge;
        
        public byte Unknown1; // TODO: What am I?

        private byte NextSequenceNumber = 0x08;

        public Tracker()
        {
            PopulateInfo();
        }

        public byte Next
        {
            get
            {
                NextSequenceNumber += 0x01;
                if (NextSequenceNumber > 0x0f)
                    NextSequenceNumber = 0x08;
                return (byte)(NextSequenceNumber | 0x30);
            }
        }

        public byte[] DumpData()
        {
            byte BurstDataType = 0x00;
            byte MemoryBank = 0x00;
            USBComm.Send(ANTCommands.ANTWrapped(new byte[] { 0x4F, 0x00, Next, 0x22, BurstDataType, 0x00, 0x00, 0x00, 0x00, 0x00 }), false);
            USBComm.WaitFor(WaitingCriteria.DeviceAck);
            USBComm.Send(ANTCommands.ANTWrapped(new byte[] { 0x4F, 0x00, Next, 0x60, 0x00, 0x02, MemoryBank, 0x00, 0x00, 0x00 }), false);
            var recv = USBComm.ReceivedMessage;
            return recv;
        }

        internal void PopulateInfo()
        {
            USBComm.Send(ANTCommands.ANTWrapped(new byte[] { 0x4F, 0x00, Next, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }), false);
            USBComm.WaitFor(WaitingCriteria.DeviceAck);
            USBComm.Send(ANTCommands.ANTWrapped(new byte[] { 0x4F, 0x00, Next, 0x70, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00 }), false);

            var infoPacket = USBComm.ReceivedMessage;
            ANTIncoming.Parse(infoPacket);

            if (infoPacket.Length < 12)
                return;

            SerialNumber = Utility.Trim(infoPacket, 0, 5);
            FirmwareVersion = infoPacket[5];
            
            BSLVersion = new Version((int)infoPacket[6], (int)infoPacket[7]);
            AppVersion = new Version((int)infoPacket[8], (int)infoPacket[9]);

            InBSLMode = infoPacket[10];

            if (infoPacket[11] == 0x00)
                DeviceOnCharge = true;
            else
                DeviceOnCharge = false;

            Unknown1 = infoPacket[12];
        }
    }
}
