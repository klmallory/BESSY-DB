using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace BESSy.Serialization
{
    public struct XmlSerializerSettings
    {
        public string XmlNameSpace { get; set; }
        public string Prefix {get; set;}
    }

    /// <summary>
    /// TODO: Create an alternate formatter from Json / Bson
    /// </summary>
    public class XmlFormatter : ISafeFormatter
    {
        static XmlSerializerSettings _defaultSettings = new XmlSerializerSettings()
        {
            XmlNameSpace = "",
            Prefix = ""
        };

        Encoding _encoding;
        XmlSerializerSettings _settings;

        public XmlFormatter() : this(Encoding.UTF8) { }

        public XmlFormatter(Encoding encoding) : this(encoding, _defaultSettings) { }

        public XmlFormatter(Encoding encoding, XmlSerializerSettings settings)
        {
            _encoding = encoding;
            _settings = settings;
        }

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            buffer = new byte[0];

            if (obj == null)
                return false;

            if (!obj.GetType().IsSerializable)
                return false;

            try
            {
                buffer = FormatObj<T>(obj);
                return true;
            }
            catch (SystemException) { return false; }
        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(buffer);
                return true;
            }
            catch (SystemException) { }

            obj = default(T);
            return false; 
        }

        public bool TryUnformatObj<T>(Stream stream, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(stream);
                return true;
            }
            catch (SystemException) { }

            obj = default(T);
            return false; 
        }
        
        public byte[] FormatObj<T>(T obj)
        {
            //Create our own namespaces for the output
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();

            //Add an empty namespace and empty value
            ns.Add(_settings.Prefix, _settings.XmlNameSpace);

            XmlSerializer xsl = new XmlSerializer(typeof(T));
            
            using (var ms = new MemoryStream())
            {
                using (var xml = new XmlTextWriter(ms, _encoding))
                {
                    xsl.Serialize(xml, obj, ns, _encoding.EncodingName);

                    return ms.ToArray();
                }
            }
        }

        public T UnformatObj<T>(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            {
                using (var xmlReader = new XmlTextReader(ms))
                {
                    XmlSerializer xsl = new XmlSerializer(typeof(T));

                    return (T)xsl.Deserialize(xmlReader, _encoding.EncodingName);
                }
            }
        }

        public T UnformatObj<T>(Stream inStream)
        {
            using (var xmlReader = new XmlTextReader(inStream))
            {
                XmlSerializer xsl = new XmlSerializer(typeof(T));

                return (T)xsl.Deserialize(xmlReader);
            }
        }
        
        public byte[] Format(byte[] buffer)
        {
            return buffer;
        }

        public Stream Format(Stream inStream)
        {
            return inStream;
        }

        public byte[] Unformat(byte[] buffer)
        {
            return buffer;
        }

        public Stream Unformat(Stream inStream)
        {
            return inStream;
        }
    }
}
