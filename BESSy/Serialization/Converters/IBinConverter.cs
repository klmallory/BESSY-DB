﻿/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Serialization.Converters
{
    public interface IBinConverter<T> : IComparer<T>
    {
        byte[] ToBytes(T item);
        T FromBytes(byte[] bytes);
        int Length { get; }

        /// <summary>
        /// compares two instances of type <typeparamref name="T"/>
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        /// <returns>returns 1 if <paramref name="item1"/> is greater than <paramref name="item2"/>,
        /// returns 0 if <paramref name="item1"/> is equal to <paramref name="item2"/>,
        /// or returns -1 if <paramref name="item1"/> is less than <paramref name="item2"/>,
        /// </returns>
        //int Compare(T item1, T item2);
    }
}