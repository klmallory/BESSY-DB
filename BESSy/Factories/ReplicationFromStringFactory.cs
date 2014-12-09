using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using BESSy.Serialization;
using System.Security;
using System.Net;
using BESSy.Replication.Tcp;
using System.Net.Sockets;

namespace BESSy.Factories
{
    public class ReplicationFactoryException : ApplicationException
    {
        public ReplicationFactoryException(string message)
            : base(message)
        {

        }
    }

    public static class ReplicationFromStringFactory
    {
        static string BAD_REPLICATION_STRING = "Replication config format error: {0}";
        static string ID_TYPE_GENERIC = "idtype parameter can not be a generic tBuilder.";


        static IList<string> paramNames = new List<string>()
        {
            "property",
            "idtype",
            "entitytype",
            "formattertype",
            "internalformattertype",
            "internalformattertype2",
            "jsonserializersettings",
            "crypto",
            "securekey",

            "ipaddress",
            "port",
            "interval",
            "authToken",
            "sendtimeout",
            "receivetimeout",
            "nodelay",
            "exclusiveaddress",
            "sendbuffersize",
            "receivebuffersize",
            "linger",
            "ipprotectionlevel",
            "dontfragment",
            "lingertime",

            "outputfilename",
            "publishertype",
            "subscribertype"
        };

        private static object ConstructIpSettings(IDictionary<string, object> typeParameters)
        {
            return new TcpSettings()
            {
                ExclusiveAddressUse = typeParameters.ContainsKey("exclusiveaddress") ? (bool)typeParameters["exclusiveaddress"] : false,
                LingerStateEnabled = typeParameters.ContainsKey("linger") ? (bool)typeParameters["linger"] : false,
                NoDelay = typeParameters.ContainsKey("nodelay") ? (bool)typeParameters["nodelay"] : false,
                ReceiveBufferSize = typeParameters.ContainsKey("receivebuffersize") ? (int)typeParameters["receivebuffersize"] : 1024,
                ReceiveTimeout = typeParameters.ContainsKey("receivetimeout") ? (int)typeParameters["receivetimeout"] : 300000,
                SendBufferSize = typeParameters.ContainsKey("sendbuffersize") ? (int)typeParameters["sendbuffersize"] : 1024,
                SendTimeout = typeParameters.ContainsKey("sendtimeout") ? (int)typeParameters["sendtimeout"] : 300000
            };
        }

        private static object ConstructIpAddress(IDictionary<string, object> typeParameters)
        {
            if (typeParameters.ContainsKey("ipaddress"))
            {
                IPAddress ip;
                return IPAddress.TryParse(typeParameters["ipaddress"] as string, out ip) ? ip : null;
            }

            return null;
        }

        private static dynamic ConstructPublisherFrom(Type pubType, IDictionary<string, Object> typeParameters)
        {
            var formatter = DatabaseFromStringFactory.ConstructFormatter(typeParameters) as IQueryableFormatter;
            var ip = ConstructIpAddress(typeParameters);
            var ipSettings = ConstructIpSettings(typeParameters);

            if (ip != null && typeParameters.ContainsKey("port") && typeParameters.ContainsKey("interval") && ipSettings != null && formatter != null)
                return Activator.CreateInstance(pubType, ip, (int)typeParameters["port"], (int)typeParameters["interval"], formatter, ipSettings);
            else if (ip != null && typeParameters.ContainsKey("port") && typeParameters.ContainsKey("interval") && ipSettings != null)
                return Activator.CreateInstance(pubType, ip, (int)typeParameters["port"], (int)typeParameters["interval"], ipSettings);
            else if (ip != null && typeParameters.ContainsKey("port"))
                return Activator.CreateInstance(pubType, ip, (int)typeParameters["port"]);

            var format = GetErrorMessage(typeParameters);

            throw new ReplicationFactoryException(string.Format("constructor for typeParameters" +  format + " not found", typeParameters));
        }

        private static string GetErrorMessage(IDictionary<string, Object> typeParameters)
        {
            string format = string.Empty;
            int count = 0;

            typeParameters.ToList().ForEach(a => format += "{" + count + "},");

            if (format.Length > 0)
                return format.Remove(format.Length - 1);
            else 
                return format;
        }

        private static dynamic ConstructSubscriberFrom(Type subType, IDictionary<string, Object> typeParameters)
        {
            var formatter = DatabaseFromStringFactory.ConstructFormatter(typeParameters) as IQueryableFormatter;
            var ipSettings = ConstructListnerSettings(typeParameters);

            if (typeParameters.ContainsKey("port") && ipSettings != null && formatter != null)
                return Activator.CreateInstance(subType, (int)typeParameters["port"], ipSettings, formatter);
            if (typeParameters.ContainsKey("port") && ipSettings != null)
                return Activator.CreateInstance(subType, (int)typeParameters["port"], ipSettings);

            var format = GetErrorMessage(typeParameters);

            throw new ReplicationFactoryException(string.Format("constructor for typeParameters: " + format + " not found", typeParameters));
        }

        private static object ConstructListnerSettings(IDictionary<string, object> typeParameters)
        {
            IPProtectionLevel level;

            return new TcpListenerSettings()
            {
                IpProtectionLevel = typeParameters.ContainsKey("ipprotectionlevel") && Enum.TryParse((string)typeParameters["ipprotectionlevel"], out level) ? level : IPProtectionLevel.Unrestricted,
                ExclusiveAddressUse = typeParameters.ContainsKey("exclusiveaddress") ? (bool)typeParameters["exclusiveaddress"] : false,
                DontFragment = typeParameters.ContainsKey("dontfragment") ? (bool)typeParameters["dontfragment"] : false,
                Linger = typeParameters.ContainsKey("linger") ? (bool)typeParameters["linger"] : false,
                LingerTime = typeParameters.ContainsKey("lingertime") ? (int)typeParameters["lingertime"] : 3
            };
        }

        public static object Create(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Create-string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = DatabaseFromStringFactory.GetParameters(createString);

            DatabaseFromStringFactory.LoadAssemblies(parameters);

            string[] types;

            foreach (var p in parameters.Where(p => paramNames.Contains(p.Key)))
            {
                switch (p.Key)
                {
                    case "outputfilename":
                        typeParameters.Add(p.Key, p.Value);
                        break;
                    case "formattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add(p.Key, DatabaseFromStringFactory.GetType(types));
                        break;
                    case "internalformattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add(p.Key, DatabaseFromStringFactory.GetType(types));
                        break;
                    case "internalformattertype2":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add(p.Key, DatabaseFromStringFactory.GetType(types));
                        break;
                    case "jsonserializersettings":
                        typeParameters.Add(p.Key, p.Value);
                        break;
                    case "crypto":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add(p.Key, DatabaseFromStringFactory.GetType(types));
                        break;
                    case "securekey":
                        var key = new SecureString();
                        foreach (var s in p.Value)
                            key.AppendChar(s);

                        typeParameters.Add(p.Key, key);
                        break;
                    case "ipaddress":
                        typeParameters.Add(p.Key, p.Value);
                        break;
                    case "port":
                        int port;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out port) ? port : 0);
                        break;
                    case "interval":
                        int interval;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out interval) ? interval : 0);
                        break;
                    case "authToken":
                        Guid auth;
                        typeParameters.Add(p.Key, Guid.TryParse(p.Value, out auth) ? auth : Guid.Empty);
                        break;
                    case "sendtimeout":
                        int sendtimeout;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out sendtimeout) ? sendtimeout : 0);
                        break;
                    case "receivetimeout":
                        int receivetimeout;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out receivetimeout) ? receivetimeout : 0);
                        break;
                    case "nodelay":
                        bool nodelay;
                        typeParameters.Add(p.Key, bool.TryParse(p.Value, out nodelay) ? nodelay : false);
                        break;
                    case "exclusiveaddress":
                        bool exclusive;
                        typeParameters.Add(p.Key, bool.TryParse(p.Value, out exclusive) ? exclusive : true);
                        break;
                    case "sendbuffersize":
                        int sendbuffersize;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out sendbuffersize) ? sendbuffersize : 0);
                        break;
                    case "receivebuffersize":
                        int receivebuffersize;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out receivebuffersize) ? receivebuffersize : 0);
                        break;
                    case "linger":
                        bool linger;
                        typeParameters.Add(p.Key, bool.TryParse(p.Value, out linger) ? linger : false);
                        break;
                    case "ipprotectionlevel":
                        typeParameters.Add(p.Key, p.Value);
                        break;
                    case "dontfragment":
                        bool dontfragment;
                        typeParameters.Add(p.Key, bool.TryParse(p.Value, out dontfragment) ? dontfragment : true);
                        break;
                    case "lingertime":
                        int lingertime;
                        typeParameters.Add(p.Key, int.TryParse(p.Value, out lingertime) ? lingertime : 0);
                        break;
                    default:
                        break;
                }
            }

            if (parameters.ContainsKey("publishertype"))
            {
                var pubType = DatabaseFromStringFactory.GetType(new string[] { (string)parameters["publishertype"] });
                return ConstructPublisherFrom(pubType, typeParameters);
            }
            else if (parameters.ContainsKey("subscribertype"))
            {
                var subType = DatabaseFromStringFactory.GetType(new string[] { (string)parameters["subscribertype"] });
                return ConstructSubscriberFrom(subType, typeParameters);
            }
            else
                throw new ReplicationFactoryException("no replication tBuilder parameter");
        }

        public static string GetNameFrom(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Create string was null or empty.");

            var parameters = DatabaseFromStringFactory.GetParameters(createString);

            return parameters.FirstOrDefault(p => p.Key == "property").Value;
        }
    }
}
