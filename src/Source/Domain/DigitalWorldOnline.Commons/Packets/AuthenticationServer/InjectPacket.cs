using DigitalWorldOnline.Commons.Writers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Commons.Packets.AuthenticationServer;

public class InjectPacket : PacketWriter
{
    public InjectPacket(int type,string hex)
    {
        Type(type);
        WriteBytes(ToByteArray(hex));
    }


    private static byte[] ToByteArray(string hexString)
    {
        int NumberChars = hexString.Length;
        byte[] bytes = new byte[NumberChars / 2];

        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);

        return bytes;
    }

}
