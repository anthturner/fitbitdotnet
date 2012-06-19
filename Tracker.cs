using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public Tracker(byte[] infoPacket)
        {
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
