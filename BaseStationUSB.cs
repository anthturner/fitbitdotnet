using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using System.Diagnostics;

namespace FitBit.NET
{
    public enum WaitingCriteria { Unknown, Beacon, CommandOK, Acknowledgement, Reset20 }

    public class BaseStationUSB
    {
        byte[] ChannelID;
        private IUsbDevice RadioBase;
        private UsbEndpointReader Reader;
        private UsbEndpointWriter Writer;

        #region Constructor and Destructor
        public BaseStationUSB(bool passive)
        {
            // Note that the VID and PID for the test device is 10C4,84C4
            UsbDeviceFinder finder = new UsbDeviceFinder(0x10C4, 0x84C4);
            RadioBase = (IUsbDevice)UsbDevice.OpenUsbDevice(finder);

            Reader = RadioBase.OpenEndpointReader(ReadEndpointID.Ep01);
            Writer = RadioBase.OpenEndpointWriter(WriteEndpointID.Ep01);
            Reader.Flush();

            RadioBase.SetConfiguration(1);
            RadioBase.ClaimInterface(0);

            if (!passive)
                RunBaseStationInit();
        }

        ~BaseStationUSB()
        {
            if (RadioBase != null)
            {
                if (RadioBase.IsOpen)
                {
                    Reader.Flush();
                    Reader.Dispose();
                    Writer.Dispose();

                    RadioBase.ReleaseInterface(0);
                    RadioBase.Close();
                }
            }
            UsbDevice.Exit();
        }
        #endregion

        public void RunBaseStationInit()
        {
            #region Set up baud rate and reset device buffers
            Debug.WriteLine("@ Setting up device communication");
            ControlWrite(0x00, 0xFFFF, 0x0, null, 0);
            ControlWrite(0x01, 0x2000, 0x0, null, 0);

            ControlWrite(0x00, 0x00, 0x0, null, 0);
            ControlWrite(0x00, 0xFFFF, 0x0, null, 0);
            ControlWrite(0x01, 0x2000, 0x0, null, 0);
            ControlWrite(0x01, 0x4A, 0x0, null, 0);

            var buffer = new byte[64];
            int len = ControlRead(0xff, 0x370B, 0, ref buffer);

            if (len < 1)
                throw new AntInitializerException("ANT Initializer failed; confirmation buffer empty.");
            if (buffer[0] != 0x02)
                throw new AntInitializerException("ANT Initializer failed; confirmation buffer expected 0x02, got 0x" + Utility.StringValueOfByte(buffer[0]) + ".");

            ControlWrite(0x03, 0x800, 0x0, null, 0);

            buffer = new byte[16];
            buffer[0] = 0x08;
            buffer[4] = 0x40;

            ControlWrite(0x13, 0x0, 0x0, buffer, 16);
            ControlWrite(0x12, 0x0c, 0x0, null, 0);
            #endregion

            // Set up ANT baseband
            Debug.WriteLine("@ Setting up ANT baseband communication");
            SetANTBaseChannel(new byte[] { 0xff, 0xff, 0x01, 0x01 });

            // Wait for our first beacon frame
            Debug.WriteLine("@ Waiting for beacon sync");
            WaitFor(WaitingCriteria.Beacon, 150);
            
            // Reset the tracker
            Debug.WriteLine("@ Resetting tracker device");
            Send(ANT.Reset4F(), false);
            WaitFor(WaitingCriteria.Acknowledgement);

            // Different content? ...
            // EX: 06/19 16:28:56 >>>>> SEND 0x78, 0x02, 0x3e, 0xd9, 0x03, 0x60, 0xc8, 0x66
            // EX: 06/19 17:24:55 >>>>> SEND 0x78, 0x02, 0xbe, 0xc3, 0x71, 0xf4, 0x6b, 0x01

            // This appears to be the channel hop request, but docs specify last 4 bytes as 0x00. This
            //   should be looked into.
            Debug.WriteLine("@ Sending link request packet.");
            Send(ANT.ANTWrapped(new byte[] { 0x4f, 0x00, 0x78, 0x02, 0xBE, 0xC3, 0x71, 0xF4, 0x6B, 0x01 }), false);
            ChannelID = new byte[] { 0xBE, 0xC3 };
            WaitFor(WaitingCriteria.Acknowledgement);

            /*
            !!! THESE TWO ARE DEPRECATED BY THE ABOVE LINK REQUEST !!!
             
            // Get a channel ID
            Debug.WriteLine("@ Grabbing random channel");
            ChannelID = Utility.RandomChannelID;
            
            // Transmit the channel ID
            Debug.WriteLine("@ Resetting communication channel");
            Send(ANT.DeviceIDReset4F(ChannelID));
            */

            // Close the baseband channel
            Debug.WriteLine("@ Closing baseband channel");
            // Should ellicit an OK
            Send(ANT.CloseChannel(), false);
            WaitFor(WaitingCriteria.CommandOK);
            
            // Reopen the device on the new channel
            Debug.WriteLine("@ Reassociating on new channel");
            SetANTBaseChannel(new byte[] { ChannelID[0], ChannelID[1], 0x01, 0x01 });
            
            // Wait for a new beacon
            Debug.WriteLine("@ Waiting for new channel beacon sync");
            WaitFor(WaitingCriteria.Beacon);
        }

        internal void SetANTBaseChannel(byte[] channel)
        {
            // Anything that is waiting for ACK needs to be sent as 4F

            Send(ANT.Reset(), false);
            System.Threading.Thread.Sleep(600); // Reset requires minimum of 0.6s to settle
            WaitFor(WaitingCriteria.Reset20);
            Send(ANT.SendNetworkKey(0x00, new byte[8]));
            Send(ANT.AssignChannel());
            Send(ANT.SetChannelPeriod(new byte[] { 0x0, 0x10 }));
            Send(ANT.SetChannelFrequency(0x2));
            Send(ANT.SetTransmitPower(0x3));
            Send(ANT.SetSearchTimeout(0xff));
            Send(ANT.SetChannelId(channel));
            Send(ANT.OpenChannel());
            // Get the confirmation back and then nothing ... WTF?
        }

        public Tracker GetTrackerInfo()
        {
            // TODO: Byte 3 (of the payload) is 0x3W (where W is a sequence number, wrapping 0xF to 0x8, starts at 0x9
            SendANT(new byte[] { 0x4F, 0x00, 0x39, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            var msg = ReceivedMessage;
            ANTIncoming.Parse(msg);
            return new Tracker(msg);
        }

        #region Reading Baselines
        internal bool WaitFor(WaitingCriteria criteria, int spinCount=75)
        {
            for (int i = 0; i < spinCount; i++)
            {
                var buffer = ReceivedMessage;
                if (buffer == null || buffer.Length < 1)
                    continue;

                switch (criteria)
                {
                    case WaitingCriteria.Beacon:
                        if (buffer[2] == 0x4E)
                            return true;
                        break;
                    case WaitingCriteria.CommandOK:
                        if (buffer[2] == 0x40 && buffer[5] == 0x0)
                            return true;
                        break;
                    case WaitingCriteria.Reset20:
                        if (buffer.Length > 3 && buffer[2] == 0x6f && buffer[3] == 0x20)
                            return true;
                        break;
                    case WaitingCriteria.Acknowledgement:
                        if (buffer.Length > 5 && buffer[2] == 0x40)
                        {
                            switch (buffer[5])
                            {
                                case 0x0a: // start
                                    continue;
                                case 0x05: // success
                                    return true;
                                case 0x06: // fail
                                    throw new Exception("Transmission failed");
                            }
                        }
                        break;
                }
            }

            return false;

            /*
            switch (criteria)
            {
                case WaitingCriteria.Beacon:
                    throw new AntInitializerException("No beacon frame detected to establish channel hop.");
                case WaitingCriteria.Acknowledgement:
                    throw new AntInitializerException("No acknowledgement received for last command.");
                case WaitingCriteria.CommandOK:
                    throw new AntInitializerException("No confirmation received for last command.");
                case WaitingCriteria.Reset20:
                    throw new AntInitializerException("No 0x20 RESET confirmation received.");
                case WaitingCriteria.Unknown:
                    throw new AntInitializerException("Unknown error. Programmer probably idiot.");
            }
             */
        }

        internal byte[] ReceivedMessage
        {
            get
            {
                byte[] buffer = new byte[64];
                PinnedHandle ph = new PinnedHandle(buffer);
                int xferLen = 0;
                var error = Reader.Read(ph.Handle, 0, 64, 1000, out xferLen);
                if (error != ErrorCode.Success && error != ErrorCode.IoTimedOut)
                {
                    Debug.WriteLine("ReceivedMessage[] Errored: " + error.ToString());
                    if (error == ErrorCode.Win32Error)
                        Debug.WriteLine("Win32Error was: " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }
                if (xferLen > 0)
                {
                    var trimBuffer = Utility.Trim(buffer, 0, xferLen);
                    ANTIncoming.Parse(trimBuffer);
                    return trimBuffer;
                }
                return new byte[0];
            }
        }
        #endregion

        #region Writing Baselines
        internal void SendANT(byte[] buffer, bool waitForCommandOk = true)
        {
            Send(ANT.ANTWrapped(buffer), waitForCommandOk);
        }
        internal void Send(byte[] buffer, bool waitForCommandOk=true)
        {
            Debug.WriteLine("Send() writing out: { " + Utility.StringValueOfByte(buffer, ' ') + " }");
            int xferLen = 0;
            Writer.Write(buffer, 3000, out xferLen);
            if (xferLen < buffer.Length)
                throw new Exception("Write failed");
            if (waitForCommandOk)
                if (!WaitFor(WaitingCriteria.CommandOK))
                {
                    Debug.WriteLine("! CommandOK not received. Retransmitting.");
                    Send(buffer, waitForCommandOk);
                }
        }
        #endregion

        #region Low-level ControlRead/ControlWrite
        internal void ControlWrite(byte request, int value, short index, byte[] data, int dataLength)
        {
            int xferLen = 0;
            UsbSetupPacket p = new UsbSetupPacket(0x40, request, (short)value, index, 100);
            RadioBase.ControlTransfer(ref p, (object)data, dataLength, out xferLen);
            Debug.WriteLine("ControlWrite() writing out: { 40 " + Utility.StringValueOfByte(request) +
                " " + Utility.StringValueOfByte(BitConverter.GetBytes((short)value), ' ') +
                " " + Utility.StringValueOfByte(BitConverter.GetBytes(index), ' ') +
                " " + (data != null && data.Length > 0 ? "[" + Utility.StringValueOfByte(data, ' ') + "]" : "[]") +
                " }");
        }

        internal int ControlRead(byte request, int value, short index, ref byte[] data)
        {
            int xferLen = 0;
            UsbSetupPacket p = new UsbSetupPacket(
                0xC0,
                request, (short)value, index, 100);
            RadioBase.ControlTransfer(ref p, (object)data, data.Length, out xferLen);
            Debug.WriteLine("ControlRead() returned: { " + Utility.StringValueOfByte(Utility.Trim(data, 0, xferLen), ' ') + " }");
            return xferLen;
        }
        #endregion
    }
}