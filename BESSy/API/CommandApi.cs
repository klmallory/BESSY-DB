using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.API
{
    public class CommandAPI
    {
        //internal struct IdKey : IComparable<IdKey>
        //{
        //    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        //    static extern int memcmp(byte[] b1, byte[] b2, long count);

        //    static int ByteArrayCompare(byte[] b1, byte[] b2)
        //    {
        //        if (b1.Length > b2.Length)
        //            return 1;
        //        else if (b1.Length < b2.Length)
        //            return -1;

        //        return memcmp(b1, b2, b1.Length);
        //    }

        //    public IdKey(object id, int typeHashCode)
        //        : this()
        //    {
        //        var key = BitConverter.GetBytes(id.GetHashCode());
        //        Key = key.Concat(BitConverter.GetBytes(typeHashCode)).ToArray();
        //    }

        //    public byte[] Key { get; private set; }

        //    public int CompareTo(IdKey other)
        //    {
        //        return ByteArrayCompare(Key, other.Key);
        //    }
        //}
    }
}
