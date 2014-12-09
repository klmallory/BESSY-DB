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
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Serialization
{
    public static class XmlSerializationHelper
    {
        static object _syncRoot = new object();
        static Dictionary<Type, XmlSerializer> _serializers = new Dictionary<Type, XmlSerializer>();

        /// <summary>
        /// Tries to serialize the specified object of the specified tBuilder, with UTF-8 as the encoding.
        /// </summary>
        /// <param property="jObj">The jObj.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns>True if the object was successfully serialized.</returns>
        public static bool TrySerialize<T>(T obj, out string output) where T : class
        {
            return TrySerialize<T>(obj, Encoding.UTF8, out output);
        }

        /// <summary>
        /// Tries to serialize the specified object of the specified tBuilder with the specified encoding.
        /// </summary>
        /// <param property="jObj">The jObj.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns>True if the object was successfully serialized.</returns>
        public static bool TrySerialize<T>(T obj, Encoding encoding, out string output) where T : class
        {
            output = string.Empty;

            if (obj == null)
                return false;

            if (!obj.GetType().IsSerializable)
                return false;

            MemoryStream ms = null;

            try
            {
                var type = typeof(T);

                if (!_serializers.ContainsKey(type))
                    lock (_syncRoot)
                        _serializers.Add(type, new XmlSerializer(type));

                XmlSerializer xsl = _serializers[type];

                ms = new MemoryStream();

                xsl.Serialize(ms, obj);

                if (!ms.CanRead)
                    return false;

                output = encoding.GetString(ms.ToArray());

                return true;
            }
            catch (InvalidOperationException iopEx)
            {
                System.Diagnostics.Trace.TraceError(iopEx.ToString());
                System.Diagnostics.Trace.TraceError("object could not be serialized: " + obj.GetType().Name);

                return false;
            }
            finally
            {
                if (ms != null)
                {
                    ms.Close();
                    ms.Dispose();
                }
            }
        }

        /// <summary>
        /// Serializes the specified object, with UTF-8 as the encoding.
        /// </summary>
        /// <param property="jObj">The object to serialize.</param>
        /// <returns>returns the serialized xml.</returns>
        public static string Serialize<T>(T obj) where T : class
        {
            return Serialize<T>(obj, Encoding.UTF8);
        }

        /// <summary>
        /// Serializes the specified object, with the specified encoding.
        /// </summary>
        /// <param property="jObj">The object to serialize.</param>
        /// <param property="encoding">The encoding.</param>
        /// <returns>returns the serialized xml.</returns>
        public static string Serialize<T>(T obj, Encoding encoding) where T : class
        {
            if (obj == null)
                throw new ArgumentNullException();

            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            using (var ms = new MemoryStream())
            {
                xsl.Serialize(ms, obj);

                string retVal = encoding.GetString(ms.ToArray());

                return retVal;
            }
        }

        /// <summary>
        /// Serializes the specified object.
        /// </summary>
        /// <param property="jObj">The objext.</param>
        /// <param property="defaultNameSpace">The default namespace.</param>
        /// <returns></returns>
        public static string Serialize<T>(T obj, string defaultNameSpace) where T : class
        {
            if (obj == null)
                throw new ArgumentNullException();

            //Create our own namespaces for the compressed
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();

            //AddOrUpdate an empty namespace and empty qVal
            ns.Add("", defaultNameSpace);

            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            using (var ms = new MemoryStream())
            {
                xsl.Serialize(ms, obj, ns);

                string retVal = Encoding.UTF8.GetString(ms.ToArray());

                return retVal;
            }
        }

        /// <summary>
        /// Tries to deserialize the specified xml as the specified inner tBuilder.
        /// </summary>
        /// <param property="xml">The XML.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns>True if the document was successfully deserialized.</returns>
        public static bool TryDeserialize<T>(string xml, out T output) where T : class
        {
            output = null;

            if (xml == null)
                return false;

            using (StringReader stringReader = new StringReader(xml))
            {
                using (XmlTextReader xmlTextReader = new XmlTextReader(stringReader))
                {
                    return TryDeserialize<T>(xmlTextReader, out output);
                }
            }
        }

        /// <summary>
        /// Tries to deserialize the specified stream as the specified inner tBuilder.
        /// </summary>
        /// <param property="stream">The stream.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns>True if the document was successfully deserialized.</returns>
        public static bool TryDeserialize<T>(Stream stream, out T output) where T : class
        {
            output = null;

            if (stream == null)
                return false;

            if (!stream.CanRead)
                return false;

            using (var xmlTextReader = new XmlTextReader(stream))
            {
                return TryDeserialize<T>(xmlTextReader, out output);
            }
        }

        /// <summary>
        /// Tries to deserialize the specified xmlTextReader as the specified inner tBuilder.
        /// </summary>
        /// <param property="xmlTextReader">The XML text reader.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns>True if the document was successfully deserialized.</returns>
        public static bool TryDeserialize<T>(XmlTextReader xmlTextReader, out T output) where T : class
        {
            return TryDeserialize((XmlReader)xmlTextReader, out output);
        }

        /// <summary>
        /// Tries the deserialize.
        /// </summary>
        /// <typeparam property="ResourceType"></typeparam>
        /// <param property="xmlReader">The XML reader.</param>
        /// <param property="compressed">The compressed.</param>
        /// <returns></returns>
        public static bool TryDeserialize<T>(XmlReader xmlReader, out T output) where T : class
        {
            output = null;

            if (xmlReader == null)
                return false;

            if (xmlReader.EOF)
                return false;

            if (!typeof(T).IsSerializable)
                return false;

            try
            {
                var type = typeof(T);

                if (!_serializers.ContainsKey(type))
                    lock (_syncRoot)
                        _serializers.Add(type, new XmlSerializer(type));

                XmlSerializer xsl = _serializers[type];

                if (!xsl.CanDeserialize(xmlReader))
                    return false;

                output = xsl.Deserialize(xmlReader) as T;

                return true;
            }
            catch (InvalidOperationException iopEx)
            {
                System.Diagnostics.Trace.TraceError(iopEx.ToString());

                return false;
            }
        }

        /// <summary>
        /// Deserializes the specified XML as the specified inner tBuilder.
        /// </summary>
        /// <param property="xml">The XML.</param>
        /// <returns>returns the deserialized object ResourceType.</returns>
        public static T Deserialize<T>(string xml) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            T retVal = xsl.Deserialize(new StringReader(xml)) as T;

            return retVal;
        }

        /// <summary>
        /// Deserializes the specified XML as the specified inner tBuilder.
        /// </summary>
        /// <param property="xml">The XML.</param>
        /// <returns>returns the deserialized object ResourceType.</returns>
        public static T Deserialize<T>(string xml, params Type[] types) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type, types));

            XmlSerializer xsl = _serializers[type];

            T retVal = xsl.Deserialize(new StringReader(xml)) as T;

            return retVal;
        }

        /// <summary>
        /// Deserializes the specified XML under the specified namespace.
        /// </summary>
        /// <param property="xml">The XML.</param>
        /// <param property="defaultNamespace">The default namespace.</param>
        /// <returns></returns>
        public static T Deserialize<T>(string xml, string defaultNamespace) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type, defaultNamespace));

            XmlSerializer xsl = _serializers[type];

            T retVal = xsl.Deserialize(new StringReader(xml)) as T;

            return retVal;
        }

        /// <summary>
        /// Deserializes the specified stream reader as ResourceType.
        /// </summary>
        /// <param property="streamReader">The stream reader.</param>
        /// <returns>returns the deserialized object ResourceType.</returns>
        public static T Deserialize<T>(StreamReader streamReader) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            XmlTextReader xmlTextReader = new XmlTextReader(streamReader.BaseStream);

            return Deserialize<T>(xmlTextReader);
        }

        /// <summary>
        /// Deserializes the specified stream as ResourceType.
        /// </summary>
        /// <param property="stream">The stream.</param>
        /// <returns>returns the deserialized object ResourceType.</returns>
        public static T Deserialize<T>(Stream stream) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            XmlTextReader xmlTextReader = new XmlTextReader(stream);

            return Deserialize<T>(xmlTextReader);
        }

        /// <summary>
        /// Deserializes the specified <see cref="XmlTextReader"/>.
        /// </summary>
        /// <typeparam property="ResourceType"></typeparam>
        /// <param property="reader">The reader.</param>
        /// <returns></returns>
        public static T Deserialize<T>(XmlTextReader reader) where T : class
        {
            return Deserialize<T>((XmlReader)reader);
        }

        /// <summary>
        /// Deserializes the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <typeparam property="ResourceType"></typeparam>
        /// <param property="reader">The reader.</param>
        /// <returns></returns>
        public static T Deserialize<T>(XmlReader reader) where T : class
        {
            var type = typeof(T);

            if (!_serializers.ContainsKey(type))
                lock (_syncRoot)
                    _serializers.Add(type, new XmlSerializer(type));

            XmlSerializer xsl = _serializers[type];

            T retVal = xsl.Deserialize(reader) as T;

            return retVal;
        }
    }
}
