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
    public enum WaitingCriteria { Unknown, Beacon, CommandOK, Acknowledgement, Reset20, DeviceAck }
    
    public static class USBComm
    {
        private static IUsbDevice RadioBase;
        private static UsbEndpointReader Reader;
        private static UsbEndpointWriter Writer;

        internal static void Init()
        {
            // Note that the VID and PID for the test device is 10C4,84C4
            UsbDeviceFinder finder = new UsbDeviceFinder(0x10C4, 0x84C4);
            USBComm.RadioBase = (IUsbDevice)UsbDevice.OpenUsbDevice(finder);

            USBComm.Reader = USBComm.RadioBase.OpenEndpointReader(ReadEndpointID.Ep01);
            USBComm.Writer = USBComm.RadioBase.OpenEndpointWriter(WriteEndpointID.Ep01);
            Reader.Flush();

            USBComm.RadioBase.SetConfiguration(1);
            USBComm.RadioBase.ClaimInterface(0);
        }

        internal static void Destroy()
        {
            if (USBComm.RadioBase != null)
            {
                if (USBComm.RadioBase.IsOpen)
                {
                    USBComm.Reader.Flush();
                    USBComm.Reader.Dispose();
                    USBComm.Writer.Dispose();

                    USBComm.RadioBase.ReleaseInterface(0);
                    USBComm.RadioBase.Close();
                }
            }
            UsbDevice.Exit();
        }

        #region Reading Baselines
        internal static bool WaitFor(WaitingCriteria criteria, int spinCount = 75)
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
                    case WaitingCriteria.DeviceAck:
                        if (buffer.Length > 5 && buffer[2] == 0x40 && buffer[5] == 0x05)
                            return true;
                        break;
                    case WaitingCriteria.CommandOK:
                        if (buffer.Length > 5 && buffer[2] == 0x40 && buffer[5] == 0x0)
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
        }

        internal static byte[] ReceivedMessage
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
        internal static void Send(byte[] buffer, bool waitForCommandOk = true)
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
        internal static void ControlWrite(byte request, int value, short index, byte[] data, int dataLength)
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

        internal static int ControlRead(byte request, int value, short index, ref byte[] data)
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
