using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using System.Diagnostics;
using FitBit.NET.ANT;

namespace FitBit.NET
{
    public class BaseStationUSB
    {
        byte[] ChannelID;

        public BaseStationUSB(bool passive)
        {
            USBComm.Init();
            if (!passive)
                RunBaseStationInit();
        }
        ~BaseStationUSB()
        {
            USBComm.Destroy();
        }

        public void RunBaseStationInit()
        {
            #region Set up baud rate and reset device buffers
            Debug.WriteLine("@ Setting up device communication");
            USBComm.ControlWrite(0x00, 0xFFFF, 0x0, null, 0);
            USBComm.ControlWrite(0x01, 0x2000, 0x0, null, 0);

            USBComm.ControlWrite(0x00, 0x00, 0x0, null, 0);
            USBComm.ControlWrite(0x00, 0xFFFF, 0x0, null, 0);
            USBComm.ControlWrite(0x01, 0x2000, 0x0, null, 0);
            USBComm.ControlWrite(0x01, 0x4A, 0x0, null, 0);

            var buffer = new byte[64];
            int len = USBComm.ControlRead(0xff, 0x370B, 0, ref buffer);

            if (len < 1)
                throw new AntInitializerException("ANT Initializer failed; confirmation buffer empty.");
            if (buffer[0] != 0x02)
                throw new AntInitializerException("ANT Initializer failed; confirmation buffer expected 0x02, got 0x" + Utility.StringValueOfByte(buffer[0]) + ".");

            USBComm.ControlWrite(0x03, 0x800, 0x0, null, 0);

            buffer = new byte[16];
            buffer[0] = 0x08;
            buffer[4] = 0x40;

            USBComm.ControlWrite(0x13, 0x0, 0x0, buffer, 16);
            USBComm.ControlWrite(0x12, 0x0c, 0x0, null, 0);
            #endregion

            // Set up ANT baseband
            Debug.WriteLine("@ Setting up ANT baseband communication");
            SetANTBaseChannel(new byte[] { 0xff, 0xff, 0x01, 0x01 });

            // Wait for our first beacon frame
            Debug.WriteLine("@ Waiting for beacon sync");
            USBComm.WaitFor(WaitingCriteria.Beacon, 150);
            
            // Reset the tracker
            Debug.WriteLine("@ Resetting tracker device");
            USBComm.Send(ANTCommands.Reset4F(), false);
            USBComm.WaitFor(WaitingCriteria.Acknowledgement);

            // Different content? ...
            // EX: 06/19 16:28:56 >>>>> SEND 0x78, 0x02, 0x3e, 0xd9, 0x03, 0x60, 0xc8, 0x66
            // EX: 06/19 17:24:55 >>>>> SEND 0x78, 0x02, 0xbe, 0xc3, 0x71, 0xf4, 0x6b, 0x01

            // This appears to be the channel hop request, but docs specify last 4 bytes as 0x00. This
            //   should be looked into.
            // Note that the channel ID should be randomized.
            Debug.WriteLine("@ Sending link request packet.");
            USBComm.Send(ANTCommands.ANTWrapped(new byte[] { 0x4f, 0x00, 0x78, 0x02, 0xBE, 0xC3, 0x71, 0xF4, 0x6B, 0x01 }), false);
            ChannelID = new byte[] { 0xBE, 0xC3 };
            USBComm.WaitFor(WaitingCriteria.Acknowledgement);

            // Close the baseband channel
            Debug.WriteLine("@ Closing baseband channel");
            // Should ellicit an OK
            USBComm.Send(ANTCommands.CloseChannel(), false);
            USBComm.WaitFor(WaitingCriteria.CommandOK);
            
            // Reopen the device on the new channel
            Debug.WriteLine("@ Reassociating on new channel");
            SetANTBaseChannel(new byte[] { ChannelID[0], ChannelID[1], 0x01, 0x01 });
            
            // Wait for a new beacon
            Debug.WriteLine("@ Waiting for new channel beacon sync");
            USBComm.WaitFor(WaitingCriteria.Beacon);
        }

        internal void SetANTBaseChannel(byte[] channel)
        {
            USBComm.Send(ANTCommands.Reset(), false);
            System.Threading.Thread.Sleep(600); // Reset requires minimum of 0.6s to settle
            USBComm.WaitFor(WaitingCriteria.Reset20);
            USBComm.Send(ANTCommands.SendNetworkKey(0x00, new byte[8]));
            USBComm.Send(ANTCommands.AssignChannel());
            USBComm.Send(ANTCommands.SetChannelPeriod(new byte[] { 0x0, 0x10 }));
            USBComm.Send(ANTCommands.SetChannelFrequency(0x2));
            USBComm.Send(ANTCommands.SetTransmitPower(0x3));
            USBComm.Send(ANTCommands.SetSearchTimeout(0xff));
            USBComm.Send(ANTCommands.SetChannelId(channel));
            USBComm.Send(ANTCommands.OpenChannel());
        }
    }
}