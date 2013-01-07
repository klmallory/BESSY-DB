/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using SevenZip;
using SevenZip.LZMA;
using SECP = System.Security.Permissions;

namespace BESSy.Serialization
{
    //[SecurityCritical()]
    //[SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    //[SECP.ReflectionPermission(SECP.SecurityAction.Demand)]
    public class BSONFormatter : ISafeFormatter
    {
        JsonSerializer _serializer;

        public BSONFormatter() : this(_defaultSettings)
        {
        }

        public BSONFormatter(JsonSerializerSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings", "Serializer settings can not be null.");

            _serializer = JsonSerializer.Create(settings);
            
            if (_serializer == null)
                throw new InvalidOperationException("Serializer could not be created.");
        }

        /// <summary>
        /// Passthrough for Bson
        /// </summary>
        /// <returns></returns>
        public byte[] Format(byte[] buffer)
        {
            return buffer;
        }

        /// <summary>
        /// Passthrough for Bson
        /// </summary>
        /// <returns></returns>
        public Stream Format(Stream inStream)
        {
            return inStream;
        }

        /// <summary>
        /// Passthrough for Bson
        /// </summary>
        /// <returns></returns>
        public byte[] Unformat(byte[] buffer)
        {
            return buffer;
        }

        /// <summary>
        /// Passthrough for Bson
        /// </summary>
        /// <returns></returns>
        public Stream Unformat(Stream inStream)
        {
            return inStream;
        }

        public byte[] FormatObj<T>(T obj)
        {
#if DEBUG
            if (object.Equals(obj, default(T)))
                throw new ArgumentNullException();
#endif

            using (var ms = new MemoryStream())
            {
                using (var bw = new BsonWriter(ms))
                {
                    _serializer.Serialize(bw, obj);

                    bw.Flush();
                }

                return ms.ToArray();
            }
        }

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            buffer = new byte[] { };

#if DEBUG
            if (object.Equals(obj, default(T)))
                return false;
#endif

            try
            {
                buffer = FormatObj<T>(obj);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }

            return false;
        }

        public T UnformatObj<T>(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            {
                return UnformatObj<T>(ms);
            }
        }

        public T UnformatObj<T>(Stream inStream)
        {
            using (var reader = new BsonReader(inStream))
            {
                T retVal = _serializer.Deserialize<T>(reader);

                return retVal;
            }
        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            obj = default(T);

            if (buffer == null)
                return false;

            try
            {
                obj = UnformatObj<T>(buffer);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }

            return false;
        }

        public bool TryUnformatObj<T>(Stream stream, out T obj)
        {
            obj = default(T);

            if (stream == null)
                return false;

            try
            {
                obj = UnformatObj<T>(stream);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }

            return false;
        }

        static readonly JsonSerializerSettings _defaultSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };

        public static JsonSerializerSettings GetDefaultSettings()
        {
            return _defaultSettings;
        }
    }
}
