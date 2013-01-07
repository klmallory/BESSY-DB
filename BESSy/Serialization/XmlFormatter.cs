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

        public XmlFormatter(Encoding encoding) : this(encoding, _defaultSettings)
        {
            
        }

        public XmlFormatter(Encoder encoding, XmlSerializerSettings settings)
        {
            _encoding = encoding;
            _settings = settings;
        }

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            output = string.Empty;

            if (obj == null)
                return false;

            if (!obj.GetType().IsSerializable)
                return false;


        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            throw new NotImplementedException();
        }

        public bool TryUnformatObj<T>(System.IO.Stream stream, out T obj)
        {
            throw new NotImplementedException();
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
            using (var xmlReader = new XmlTextReader(inStream))
            {
                XmlSerializer xsl = new XmlSerializer(typeof(T));

                return xsl.Deserialize(xmlReader, _encoding.EncodingName) as T;
            }
        }

        public T UnformatObj<T>(Stream inStream)
        {
            using (var xmlReader = new XmlTextReader(inStream))
            {
                XmlSerializer xsl = new XmlSerializer(typeof(T));

                return xsl.Deserialize(xmlReader) as T;
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
