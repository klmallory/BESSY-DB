using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security;
using System.Reflection;
using System.IO;
using BESSy.Serialization;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Seeding;
using BESSy.Serialization.Converters;

namespace BESSy.Factories
{
    public class DatabaseFactoryException : ApplicationException
    {
        public DatabaseFactoryException(string message)
            : base(message)
        {

        }
    }

    public static class DatabaseFromStringFactory
    {
        static string BAD_CONNECTION_STRING = "Connection string format error: {0}";
        static string ID_TYPE_GENERIC = "idtype parameter can not be a generic tBuilder.";

        internal static IList<string> paramNames = new List<string>()
        {
            "filename",
            "idtype",
            "idtoken",
            "entitytype",
            "formattertype",
            "internalformattertype",
            "internalformattertype2",
            "jsonserializersettings",
            "seedtype",
            "startingseed",
            "binconvertertype",
            "catalogconvertertype",
            "transactionmanagertype",
            "filefactorytype",
            "cachesize",
            "cachefactorytype",
            "indexfactorytype",
            "indexfilefactorytype",
            "crypto",
            "securekey",
            "authorizedusers",
            "username",
            "password"
        };

        internal static Type GetType(string[] typeNames)
        {
            var types = new List<Type>();

            foreach (var typeName in typeNames)
            {
                var t = Type.GetType(typeName);

                if (t == null)
                    t = Type.GetType(typeName,
                        n => AppDomain.CurrentDomain.GetAssemblies().Where(z => z.FullName == n.FullName).LastOrDefault()
                        , null, true);

                types.Add(t);
            }

            if (types.Count > 1)
                return types[0].MakeGenericType(types.Skip(1).ToArray());
            else
                return types[0];
        }

        #region Create

        internal static dynamic ConstructDbFrom(Type dbType, IDictionary<string, Object> typeParameters)
        {
            var idType = (Type)typeParameters["idtype"];
            var fileName = typeParameters["filename"];
            var idToken = typeParameters.ContainsKey("idtoken") ? typeParameters["idtoken"] : null;

            var indexFileFactory = ConstructIndexFileFactory(typeParameters);
            var indexFactory = ConstructIndexFactory(typeParameters);
            var cacheFactory = ConstructRepositoryCacheFactory(typeParameters);
            var fileManagerFactory = ConstructFileManagerFactory(typeParameters);
            var formatter = ConstructFormatter(typeParameters) as IQueryableFormatter;
            var core = ConstructCore(typeParameters);
            var transactionManager = ConstructTransactionManager(typeParameters);
            var binConverter = ConstructBinConverter(typeParameters);

            if (indexFactory != null && indexFileFactory != null && cacheFactory != null && fileManagerFactory != null && transactionManager != null && formatter != null && binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter, formatter);
            }
            else if (cacheFactory != null && fileManagerFactory != null && transactionManager != null && formatter != null && binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter, formatter);
            }
            else if (fileManagerFactory != null && transactionManager != null && formatter != null && binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter, formatter);
            }
            else if (indexFactory != null && indexFileFactory != null && cacheFactory != null && fileManagerFactory != null && formatter != null && transactionManager != null)
            {
                return Activator.CreateInstance(dbType, fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory);
            }
            else if (transactionManager != null && formatter != null && binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter, formatter);
            }
            else if (cacheFactory != null && fileManagerFactory != null && formatter != null && transactionManager != null)
            {
                return Activator.CreateInstance(dbType, fileName, formatter, transactionManager, fileManagerFactory, cacheFactory);
            }
            else if (formatter != null && binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter, formatter);
            }
            else if (fileManagerFactory != null && formatter != null && transactionManager != null)
            {
                return Activator.CreateInstance(dbType, fileName, formatter, transactionManager, fileManagerFactory);
            }
            else if (binConverter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, binConverter);
            }
            else if (formatter != null && core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core, formatter);
            }
            else if (formatter != null && transactionManager != null)
            {
                return Activator.CreateInstance(dbType, fileName, formatter, transactionManager);
            }
            else if (core != null && idToken != null)
            {
                return Activator.CreateInstance(dbType, fileName, idToken, core);
            }
            else if (formatter != null)
            {
                return Activator.CreateInstance(dbType, fileName, formatter);
            }
            else if (typeParameters.ContainsKey("idtoken"))
            {
                return Activator.CreateInstance(dbType, fileName, idToken);
            }
            else
                return Activator.CreateInstance(dbType, fileName);

            throw new DatabaseFactoryException(string.Format("constructor for typeParameters {0},{1},{2},{3},{4},{5},{6} not found", typeParameters));
        }

        internal static object GetSeedFrom(string str, Type idType)
        {
            if (idType.FullName == typeof(string).FullName)
                return str;
            if (idType.FullName == typeof(byte).FullName)
                byte.Parse(str);
            if (idType.FullName == typeof(UInt16).FullName)
                UInt16.Parse(str);
            if (idType.FullName == typeof(UInt32).FullName)
                UInt32.Parse(str);
            if (idType.FullName == typeof(Int32).FullName)
                return Int32.Parse(str);
            if (idType.FullName == typeof(UInt64).FullName)
                return UInt64.Parse(str);
            if (idType.FullName == typeof(Int64).FullName)
                return Int64.Parse(str);
            if (idType.FullName == typeof(float).FullName)
                return float.Parse(str);
            if (idType.FullName == typeof(double).FullName)
                return double.Parse(str);
            if (idType.FullName == typeof(decimal).FullName)
                return decimal.Parse(str);
            if (idType.FullName == typeof(DateTime).FullName)
                return DateTime.Parse(str);
            if (idType.FullName == typeof(Guid).FullName)
                return new Guid(str);

            return JObject.Parse(str).ToObject(idType);
        }

        internal static object ConstructCore(IDictionary<string, object> typeParameters)
        {
            object core = null;
            object seed = null;

            var idType = (Type)typeParameters["idtype"];

            var type = Type.GetType("BESSy.Seeding.FileCore`2").MakeGenericType(idType, typeof(Int64));

            if (typeParameters.ContainsKey("seedtype"))
            {
                var seedType = (Type)typeParameters["seedtype"];

                if (typeParameters.ContainsKey("startingseed"))
                {
                    var startingSeed = GetSeedFrom(typeParameters["startingseed"] as string, (Type)typeParameters["idtype"]);
                    
                    seed = Activator.CreateInstance(seedType, startingSeed);

                }
                else
                    seed = Activator.CreateInstance(seedType);

                if (seed == null)
                    throw new DatabaseFactoryException("failed to create core from parameters.");
            }

            core = Activator.CreateInstance(type, seed);

            return core;
        }

        internal static object ConstructBinConverter(IDictionary<string, object> typeParameters)
        {
            object bct = null;

            if (typeParameters.ContainsKey("binconvertertype"))
            {
                var bctType = (Type)typeParameters["binconvertertype"];

                bct = Activator.CreateInstance(bctType);

                if (bct == null)
                    throw new DatabaseFactoryException("failed to create binConverter from parameters.");
            }

            return bct;
        }

        internal static IQueryableFormatter ConstructFormatter(IDictionary<string, Object> typeParameters)
        {
            IQueryableFormatter formatter = null;

            if (typeParameters.ContainsKey("formattertype"))
            {
                var fmtType = (Type)typeParameters["formattertype"];
                JsonSerializerSettings settings = null;
                IQueryableFormatter intFmt = null;
                IQueryableFormatter intFmt2 = null;

                if (typeParameters.ContainsKey("jsonserializersettings"))
                    settings = JsonConvert.DeserializeObject<JsonSerializerSettings>((string)typeParameters["jsonserializersettings"], JSONFormatter.GetDefaultSettings());

                if (typeParameters.ContainsKey("internalformattertype2"))
                {
                    if (settings != null)
                        intFmt2 = Activator.CreateInstance((Type)typeParameters["internalformattertype2"], settings) as IQueryableFormatter;
                    else
                        intFmt2 = Activator.CreateInstance((Type)typeParameters["internalformattertype2"]) as IQueryableFormatter;
                }

                if (typeParameters.ContainsKey("internalformattertype"))
                {
                    if (intFmt2 != null)
                    {
                        //zip should come before crypto for practical reasons, compressing encrypted bytes has little reasons.
                        intFmt = Activator.CreateInstance((Type)typeParameters["internalformattertype"], intFmt2) as IQueryableFormatter;
                    }
                    else
                    {
                        if (settings != null)
                            intFmt = Activator.CreateInstance((Type)typeParameters["internalformattertype"], settings) as IQueryableFormatter;
                        else
                            intFmt = Activator.CreateInstance((Type)typeParameters["internalformattertype"]) as IQueryableFormatter;
                    }
                }

                if (typeParameters.ContainsKey("securekey") && typeParameters.ContainsKey("crypto") && intFmt != null)
                {
                    if (intFmt != null)
                    {
                        var crypto = Activator.CreateInstance((Type)typeParameters["crypto"], (SecureString)typeParameters["securekey"]);

                        formatter = Activator.CreateInstance(fmtType, crypto, intFmt, (SecureString)typeParameters["securekey"]) as IQueryableFormatter;
                    }
                }
                else if (intFmt != null)
                {
                    formatter = Activator.CreateInstance((Type)typeParameters["formattertype"], intFmt) as IQueryableFormatter;
                }
                else if (settings != null)
                {
                    formatter = Activator.CreateInstance(fmtType, settings) as IQueryableFormatter;
                }
                else
                    formatter = Activator.CreateInstance(fmtType) as IQueryableFormatter;

                if (formatter == null)
                    throw new DatabaseFactoryException("failed to create formatter from parameters.");
            }

            return formatter;
        }

        internal static object ConstructTransactionManager(IDictionary<string, object> typeParameters)
        {
            object trans = null;

            if (typeParameters.ContainsKey("transactionmanagertype"))
            {
                //TODO: add alternative for trans manager constructors.
                trans = Activator.CreateInstance((Type)typeParameters["transactionmanagertype"]);

                if (trans == null)
                    throw new DatabaseFactoryException("failed to create trans managerfrom parameters.");
            }

            return trans;
        }

        internal static IIndexFactory ConstructIndexFactory(IDictionary<string, object> typeParameters)
        {
            IIndexFactory indexFactory = null;

            if (typeParameters.ContainsKey("indexfactorytype"))
            {
                indexFactory = Activator.CreateInstance((Type)typeParameters["indexfactorytype"]) as IIndexFactory;

                if (indexFactory == null)
                    throw new DatabaseFactoryException("failed to create location factory from parameters.");
            }

            return indexFactory;
        }

        internal static IIndexFileFactory ConstructIndexFileFactory(IDictionary<string, object> typeParameters)
        {
            IIndexFileFactory indexFileFactory = null;

            if (typeParameters.ContainsKey("indexfilefactorytype"))
            {
                indexFileFactory = Activator.CreateInstance((Type)typeParameters["indexfilefactorytype"]) as IIndexFileFactory;

                if (indexFileFactory == null)
                    throw new DatabaseFactoryException("failed to create location file factory from parameters.");
            }

            return indexFileFactory;
        }

        internal static IAtomicFileManagerFactory ConstructFileManagerFactory(IDictionary<string, object> typeParameters)
        {
            IAtomicFileManagerFactory fileManagerFactory = null;

            if (typeParameters.ContainsKey("filefactorytype"))
            {
                fileManagerFactory = Activator.CreateInstance((Type)typeParameters["filefactorytype"]) as IAtomicFileManagerFactory;

                if (fileManagerFactory == null)
                    throw new DatabaseFactoryException("failed to create atomic file factory from parameters.");
            }

            return fileManagerFactory;
        }

        internal static IDatabaseCacheFactory ConstructRepositoryCacheFactory(IDictionary<string, object> typeParameters)
        {
            IDatabaseCacheFactory cacheFactory = null;

            if (typeParameters.ContainsKey("cachefactorytype"))
            {
                if (typeParameters.ContainsKey("cachesize"))
                {
                    int size;

                    size = int.TryParse((String)typeParameters["cachesize"], out size) ? size : 0;

                    cacheFactory = Activator.CreateInstance((Type)typeParameters["cachefactorytype"], size) as IDatabaseCacheFactory;
                }
                else
                    cacheFactory = Activator.CreateInstance((Type)typeParameters["cachefactorytype"]) as IDatabaseCacheFactory;

                if (cacheFactory == null)
                    throw new DatabaseFactoryException("failed to create cache factory from parameters.");
            }

            return cacheFactory;
        }

        internal static IDictionary<string, string> GetParameters(string createString)
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();

            var regEx = new Regex(@"([\w|\aqn]*)=([^;]*);?", RegexOptions.IgnoreCase);

            var match = regEx.Match(createString);

            while (match.Success && match.Groups.Count == 3)
            {
                if (string.IsNullOrWhiteSpace(match.Groups[1].Value)
                    || string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    Trace.TraceError("Could not parse arguments {0} & {1}", match.Groups[1].Value, match.Groups[2].Value);
                    continue;
                }

                parameters.Add(match.Groups[1].Value.Trim().ToLower(), match.Groups[2].Value.Trim());

                match = match.NextMatch();
            }

            return parameters;
        }

        internal static void LoadAssemblies(IDictionary<string, string> parameters)
        {
            if (parameters.Any(p => string.Compare(p.Key, "assembly", true) == 0))
            {
                foreach (var parm in parameters.Where(p => string.Compare(p.Key, "assembly", true) == 0))
                {
                    foreach (var a in parm.Value.Split('|'))
                    {
                        if (Path.HasExtension(a))
                        {
                            if (File.Exists(a))
                                Assembly.LoadFrom(a);
                            else if (File.Exists(Path.Combine(".", "Assemblies", a)))
                                Assembly.LoadFile(Path.Combine(".", "Assemblies", a));
                        }
                    }
                }
            }
        }

        public static object Create(string createString)
        {
            return Create(createString, null);
        }
        public static object Create(string createString, string fileName)
        {
            return Create(createString, fileName, false);
        }
        public static object Create(string createString, string fileName, bool createCatalog)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Connection string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = GetParameters(createString);

            LoadAssemblies(parameters);

            string[] types;

            foreach (var p in parameters.Where(p => paramNames.Contains(p.Key)))
            {
                switch (p.Key)
                {
                    case "filename":
                        typeParameters.Add("filename", fileName == null ? p.Value : fileName);
                        break;
                    case "idtype":
                        var type = Type.GetType(p.Value);

                        if (type.IsGenericType)
                            throw new DatabaseFactoryException(BAD_CONNECTION_STRING + ID_TYPE_GENERIC);

                        typeParameters.Add("idtype", type);
                        break;
                    case "idtoken":
                        typeParameters.Add("idtoken", createCatalog ? "Id" : p.Value);
                        break;
                    case "entitytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("entitytype", GetType(types));
                        break;
                    case "formattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("formattertype", GetType(types));
                        break;
                    case "internalformattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("internalformattertype", GetType(types));
                        break;
                    case "internalformattertype2":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("internalformattertype2", GetType(types));
                        break;
                    case "jsonserializersettings":
                        typeParameters.Add("jsonserializersettings", p.Value);
                        break;
                    case "seedtype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("seedtype", GetType(types));
                        break;
                    case "startingseed":
                        typeParameters.Add("startingseed", p.Value);
                        break;
                    case "binconvertertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("binconvertertype", GetType(types));
                        break;
                    case "transactionmanagertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("transactionmanagertype", GetType(types));
                        break;
                    case "filefactorytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("filefactorytype", GetType(types));
                        break;
                    case "cachesize":
                        typeParameters.Add("cachesize", p.Value);
                        break;
                    case "cachefactorytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("cachefactorytype", GetType(types));
                        break;
                    case "indexfactorytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("indexfactorytype", GetType(types));
                        break;
                    case "indexfilefactorytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("indexfilefactorytype", GetType(types));
                        break;
                    case "crypto":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("crypto", GetType(types));
                        break;
                    case "securekey":
                        var key = new SecureString();
                        foreach (var s in p.Value)
                            key.AppendChar(s);

                        typeParameters.Add("securekey", key);
                        break;
                    default:
                        break;
                }
            }

            if (!typeParameters.ContainsKey("idtype"))
                throw new DatabaseFactoryException("connection string missing required field 'idtype'");
            if (!typeParameters.ContainsKey("entitytype"))
                throw new DatabaseFactoryException("connection string missing required field 'entitytype'");
            if (!typeParameters.ContainsKey("filename"))
                throw new DatabaseFactoryException("connection string missing required field 'entitytype'");

            var dbType = Type.GetType("BESSy.Database`2");
            dbType = dbType.MakeGenericType((Type)typeParameters["idtype"], (Type)typeParameters["entitytype"]);

            var database = ConstructDbFrom(dbType, typeParameters);

            return database;
        }

        #endregion

        public static string ReplaceParameter(string parameterName, string connectionString, string value)
        {
            var paramExp = @"(" + parameterName + @"\aqn)=([^;]*);?";
            var replaceExp = @"$`" + parameterName + " = " + value + @"$'";
            var regEx = new Regex(paramExp, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            var modified = regEx.Replace(connectionString, replaceExp, 1, 0);

            return modified;
        }

        public static string GetFileName(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            var parameters = GetParameters(connectionString);

            return parameters.FirstOrDefault(p => p.Key == "filename").Value;
        }

        public static string GetIdToken(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            return parameters.FirstOrDefault(p => p.Key == "idtoken").Value;
        }

        public static string GetUserName(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            return parameters.FirstOrDefault(p => p.Key == "username").Value;
        }

        public static SecureString GetPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            var pwd = parameters.FirstOrDefault(p => p.Key == "password").Value;

            var sec = new SecureString();

            foreach (var c in pwd)
                sec.AppendChar(c);

            return sec;
        }

        public static object CreateSeed(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            string[] types;

            foreach (var p in parameters)
            {
                switch (p.Key)
                {
                    case "idtype":
                        var type = Type.GetType(p.Value);

                        if (type.IsGenericType)
                            throw new DatabaseFactoryException(BAD_CONNECTION_STRING + ID_TYPE_GENERIC);

                        typeParameters.Add("idtype", type);
                        break;
                    case "seedtype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("seedtype", GetType(types));
                        break;
                    case "startingseed":
                        typeParameters.Add("startingseed", p.Value);
                        break;
                }
            }

            return ConstructCore(typeParameters);
        }

        public static object CreateConverter(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            string[] types;

            foreach (var p in parameters.Where(p => p.Key == "binconvertertype"))
            {
                switch (p.Key)
                {
                    case "binconvertertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("binconvertertype", GetType(types));
                        break;
                }
            }

            return ConstructBinConverter(typeParameters);
        }

        public static IQueryableFormatter CreateFormatter(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = GetParameters(connectionString);

            LoadAssemblies(parameters);

            string[] types;

            foreach (var p in parameters)
            {
                switch (p.Key)
                {
                    case "formattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("formattertype", GetType(types));
                        break;
                    case "internalformattertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("internalformattertype", GetType(types));
                        break;
                    case "internalformattertype2":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("internalformattertype2", GetType(types));
                        break;
                    case "jsonserializersettings":
                        typeParameters.Add("jsonserializersettings", p.Value);
                        break;
                    case "crypto":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("crypto", GetType(types));
                        break;
                    case "securekey":
                        var key = new SecureString();
                        foreach (var s in p.Value)
                            key.AppendChar(s);

                        typeParameters.Add("securekey", key);
                        break;
                }
            }

            return ConstructFormatter(typeParameters);
        }
    }
}
