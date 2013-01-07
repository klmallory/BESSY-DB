/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Extensions
{
    internal static class ClampExtensions
    {
        internal static byte Clamp(this byte val, byte min, byte max)
        {
            if (val > max)
                return max;
            else if (val < min)
                return min;
            else
                return val;
        }

        internal static int Clamp(this int val, int min, int max)
        {
            if (val > max)
                return max;
            else if (val < min)
                return min;
            else
                return val;
        }

        internal static long Clamp(this long val, long min, long max)
        {
            if (val > max)
                return max;
            else if (val < min)
                return min;
            else
                return val;
        }

    }
}
