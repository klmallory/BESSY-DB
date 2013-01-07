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
    internal static class ArrayExtensions
    {
        public static bool Equals<T>(this ArraySegment<T> segment, ArraySegment<T> compareSegment) where T: IEquatable<T>
        {
            for (var i = 0; i < segment.Count; i++)
            {
                if (!(segment.Array[segment.Offset + i].Equals(compareSegment.Array[compareSegment.Offset + i])))
                    return false;
            }

            return true;
        }
    }
}
