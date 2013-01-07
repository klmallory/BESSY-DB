/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BESSy.Serialization
{
    public interface ILinqFormatter : IFormatter
    {
        IEnumerable<JArray> AsQueryable();
    }

    public interface ISafeFormatter : IFormatter
    {
        bool TryFormatObj<T>(T obj, out byte[] buffer);
        bool TryUnformatObj<T>(byte[] buffer, out T obj);
        bool TryUnformatObj<T>(Stream stream, out T obj);
    }

    public interface IFormatter : IBinFormatter
    {
        byte[] FormatObj<T>(T obj);
        T UnformatObj<T>(byte[] buffer);
        T UnformatObj<T>(Stream inStream);
    }

    public interface IBinFormatter
    {
        byte[] Format(byte[] buffer);
        System.IO.Stream Format(Stream inStream);
        byte[] Unformat(byte[] buffer);
        System.IO.Stream Unformat(Stream inStream);
    }


}
