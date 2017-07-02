using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public static class GUIDHelper
    {
        public static Guid CreateDerivedGuid(Guid orig, int no, bool longRange = false)
        {
            byte[] a = orig.ToByteArray();
            byte[] b = new byte[16];

            for (int i = 0; i < 8; i++)
                b[i] = (byte)no;

            if (no > 255)
            {
                int divVal = no / 255;
                b[0] = (byte)divVal;
                b[0] += 128;
                b[1] = (byte)divVal;
            }

            if (!longRange)
            {
                // XOR
                return new Guid(BitConverter.GetBytes(BitConverter.ToUInt64(a, 0) ^ BitConverter.ToUInt64(b, 8))
                    .Concat(BitConverter.GetBytes(BitConverter.ToUInt64(a, 8) ^ BitConverter.ToUInt64(b, 0))).ToArray());
            }
            else
            {

                // AND
                for (int i = 0; i < 16; i++)
                    b[i] = (byte)no;

                int div = no / 255;
                b[0] = (byte)div;
                b[2] = (byte)div;
                b[4] = (byte)div;
                b[6] = (byte)div;
                b[8] = (byte)div;
                b[10] = (byte)div;
                b[12] = (byte)div;
                b[14] = (byte)div;

                for (int i = 0; i < 16; i++)
                    a[i] += b[i];

                return new Guid(a);
            }
        }
    }
}
