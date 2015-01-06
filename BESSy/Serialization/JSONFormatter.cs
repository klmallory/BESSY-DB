/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Json.Serialization;
using BESSy.Factories;

namespace BESSy.Serialization
{
    public class JSONFormatter : IQueryableFormatter
    {
        public JSONFormatter()
            : this(GetDefaultSettings())
        {
        }

        public JSONFormatter(JsonSerializerSettings settings)
        {
            if (_settings == null)
                _settings = settings;

            _serializer = JsonSerializer.Create(_settings);
        }

        protected JsonSerializer _serializer;

        protected JsonSerializerSettings _settings;

        public JsonSerializerSettings Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                _settings = value;
                _serializer = JsonSerializer.Create(value);
            }
        }

        public virtual bool Trim { get { return false; } }
        public virtual ArraySegment<byte> TrimMarker { get { return new ArraySegment<byte>(); } }

        /// <summary>
        /// Passthrough for Json
        /// </summary>
        /// <returns></returns>
        public byte[] Format(byte[] buffer)
        {
            return buffer;
        }

        /// <summary>
        /// Passthrough for Json
        /// </summary>
        /// <returns></returns>
        public Stream Format(Stream inStream)
        {
            return inStream;
        }

        /// <summary>
        /// Passthrough for Json
        /// </summary>
        /// <returns></returns>
        public byte[] Unformat(byte[] buffer)
        {
            return buffer;
        }

        /// <summary>
        /// Passthrough for Json
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

            return ((MemoryStream)FormatObjStream(obj)).ToArray();
        }

        public Stream FormatObjStream<T>(T obj)
        {
#if DEBUG
            if (object.Equals(obj, default(T)))
                throw new ArgumentNullException();
#endif
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);
            var jw = new JsonTextWriter(sw);

            _serializer.Serialize(jw, obj);

            jw.Flush();
            sw.Flush();
            
            return ms;
        }

        public bool TryFormatObj<T>(T obj, out Stream outStream)
        {
            outStream = null;

            if (object.Equals(obj, default(T)))
                return false;

            try
            {
                outStream = FormatObjStream<T>(obj);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }
            catch (ApplicationException) { }

            return false;
        }

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            buffer = new byte[] { };

            if (object.Equals(obj, default(T)))
                return false;

            try
            {
                buffer = ((MemoryStream)FormatObjStream<T>(obj)).ToArray();

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }
            catch (ApplicationException) { }

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
            inStream.Position = 0;

            var sr = new StreamReader(inStream);
            var jr = new JsonTextReader(sr);

            return _serializer.Deserialize<T>(jr);
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
            catch (ApplicationException) { }

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
            catch (ApplicationException) { }

            return false;
        }

        #region IQueryableFormatter Memebers

        public JsonSerializer Serializer { get { return _serializer; } }

        public JObject AsQueryableObj<T>(T obj)
        {
            if (obj != null)
                return JObject.FromObject(obj, _serializer);
            else
                return new JObject();
        }

        public JObject Parse(Stream inStream)
        {
            inStream.Position = 0;
            var sr = new StreamReader(inStream);
            var reader = new JsonTextReader(sr);
            return JObject.Load(reader);
        }

        public Stream Unparse(JObject token)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);
            var jw = JsonWriterFactory.CreateFrom(sw, _settings); // new JsonTextWriter(sw)

            token.WriteTo(jw, _serializer.Converters.ToArray());

            jw.Flush();
            sw.Flush();

            return ms;
        }

        public bool TryParse(Stream inStream, out JObject obj)
        {
            try
            {
                obj = Parse(inStream);
                return true;
            }
            catch (Exception) { obj = null; return false; }
        }

        public bool TryUnparse(JObject token, out Stream stream)
        {
            try
            {
                stream = Unparse(token);
                return true;
            }
            catch (Exception) { stream = null; return false; }
        }

        #endregion

        internal static readonly JsonSerializerSettings _defaultSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ContractResolver = new DefaultContractResolver() { IgnoreSerializableInterface = true },
            Formatting = Formatting.None,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameHandling = TypeNameHandling.Objects,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
        };

        public static JsonSerializerSettings GetDefaultSettings()
        {
            return _defaultSettings;
        }
    }
}
