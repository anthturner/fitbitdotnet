using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FitBit.NET
{
    public class PacketTooLongException : Exception
    {
        public PacketTooLongException()
        {
        }

        public PacketTooLongException(string message)
            : base(message)
        {
        }
    }

    public class AntInitializerException : Exception
    {
        public AntInitializerException()
        {
        }

        public AntInitializerException(string message)
            : base(message)
        {
        }
    }
}
