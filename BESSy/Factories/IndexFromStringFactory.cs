using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using System.IO;

namespace BESSy.Factories
{
    public class IndexFactoryException : ApplicationException
    {
        public IndexFactoryException(string message)
            : base(message)
        {

        }
    }

    public static class IndexFromStringFactory
    {
        static string BAD_INDEX_STRING = "Index config format error: {0}";
        static string ID_TYPE_GENERIC = "idtype parameter can not be a generic tBuilder.";

        static IList<string> paramNames = new List<string>()
        {
            "filename",
            "idtype",
            "entitytype",
            "segmenttype",
            "indextoken",
            "startingsize",
            "isunique",
            "indexconvertertype",
            "segmentconvertertype",
            "segmentsynchronizertype",
            "pagesynchronizertype",
            "indexfactorytype"
        };

        public static object Create(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Create-string was null or empty.");

            IDictionary<string, object> typeParameters = new Dictionary<string, object>();

            var parameters = DatabaseFromStringFactory.GetParameters(createString);

            DatabaseFromStringFactory.LoadAssemblies(parameters);

            string[] types;

            Type factoryType = null;

            foreach (var p in parameters.Where(p => paramNames.Contains(p.Key)))
            {
                switch (p.Key)
                {
                    case "filename":
                        typeParameters.Add("filename", p.Value);
                        break;
                    case "idtype":
                        var type = Type.GetType(p.Value);

                        if (type.IsGenericType)
                            throw new ReplicationFactoryException(BAD_INDEX_STRING + ID_TYPE_GENERIC);

                        typeParameters.Add("idtype", type);
                        break;
                    case "entitytype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("entitytype", DatabaseFromStringFactory.GetType(types));
                        break;
                    case "segmenttype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("segmenttype", DatabaseFromStringFactory.GetType(types));
                        break;
                    case "indextoken":
                        typeParameters.Add("indextoken", p.Value);
                        break;
                    case "isunique":
                        bool val;
                        typeParameters.Add("isunique", bool.TryParse(p.Value, out val) ? val : false);
                        break;
                    case "startingsize":
                        int sze;
                        typeParameters.Add("startingsize", int.TryParse(p.Value, out sze) ? sze : 0);
                        break;
                    case "indexconvertertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("indexconvertertype", DatabaseFromStringFactory.GetType(types));
                        break;
                    case "segmentconvertertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("segmentconvertertype", DatabaseFromStringFactory.GetType(types));
                        break;
                    case "segmentsynchronizertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("segmentsynchronizertype", DatabaseFromStringFactory.GetType(types));
                        break;
                    case "pagesynchronizertype":
                        types = p.Value.Split(new string[] { "|" }, StringSplitOptions.None);
                        typeParameters.Add("pagesynchronizertype", DatabaseFromStringFactory.GetType(types));
                        break;
                    default:
                        break;
                }
            }

            if (!typeParameters.ContainsKey("idtype"))
                throw new ReplicationFactoryException("create string missing required field 'idtype'");
            if (!typeParameters.ContainsKey("entitytype"))
                throw new ReplicationFactoryException("create string missing required field 'entitytype'");
            if (!typeParameters.ContainsKey("segmenttype"))
                throw new ReplicationFactoryException("create string missing required field 'segmenttype'");
            if (!typeParameters.ContainsKey("filename"))
                throw new ReplicationFactoryException("create string missing required field 'entitytype'");
            else
                throw new ReplicationFactoryException("no known index tBuilder");

            var index = ConstructIndexFrom(typeParameters);
        }

        internal static dynamic ConstructIndexFrom(IDictionary<string, Object> typeParameters)
        {
            var indexType = Type.GetType("BESSy.Indexes.Index`3");
            indexType = indexType.MakeGenericType((Type)typeParameters["idtype"], (Type)typeParameters["entitytype"], (Type)typeParameters["segmenttype"]);

            var indexConverter = ConstructIndexConverter(typeParameters);
            var segmentConverter = ConstructSegmentConverter(typeParameters);

            var segmentSync = ConstructSegmentSynchronizer(typeParameters);
            var pageSync = ConstructPageSynchronizer(typeParameters);

            if (indexConverter != null && segmentConverter != null && segmentSync != null && pageSync != null && typeParameters.ContainsKey("startingsize"))

                return Activator.CreateInstance(indexType,
                    (string)typeParameters["filename"],
                    (string)typeParameters["indextoken"],
                    (bool)typeParameters["isunique"],
                    (int)typeParameters["startingsize"],
                    indexConverter,
                    segmentConverter,
                    segmentSync,
                    pageSync);

            else
                return Activator.CreateInstance(indexType,
                    (string)typeParameters["filename"],
                    (string)typeParameters["indextoken"],
                    (bool)typeParameters["isunique"]);
        }

        internal static object ConstructIndexConverter(IDictionary<string, object> typeParameters)
        {
            object bct = null;

            if (typeParameters.ContainsKey("indexconvertertype"))
            {
                var bctType = (Type)typeParameters["indexconvertertype"];

                bct = Activator.CreateInstance(bctType);

                if (bct == null)
                    throw new DatabaseFactoryException("failed to create binConverter from parameters.");
            }

            return bct;
        }

        internal static object ConstructSegmentConverter(IDictionary<string, object> typeParameters)
        {
            object bct = null;

            if (typeParameters.ContainsKey("segmentconvertertype"))
            {
                var bctType = (Type)typeParameters["segmentconvertertype"];

                bct = Activator.CreateInstance(bctType);

                if (bct == null)
                    throw new DatabaseFactoryException("failed to create binConverter from parameters.");
            }

            return bct;
        }

        internal static object ConstructSegmentSynchronizer(IDictionary<string, object> typeParameters)
        {
            object segSync = null;

            if (typeParameters.ContainsKey("segmentconvertertype"))
            {
                var syncType = (Type)typeParameters["segmentconvertertype"];

                segSync = Activator.CreateInstance(syncType);

                if (segSync == null)
                    throw new DatabaseFactoryException("failed to create row synchronizer from parameters.");
            }

            return segSync;
        }

        internal static object ConstructPageSynchronizer(IDictionary<string, object> typeParameters)
        {
            object segSync = null;

            if (typeParameters.ContainsKey("pagesynchronizertype"))
            {
                var syncType = (Type)typeParameters["pagesynchronizertype"];

                segSync = Activator.CreateInstance(syncType);

                if (segSync == null)
                    throw new DatabaseFactoryException("failed to create row synchronizer from parameters.");
            }

            return segSync;
        }

        public static string GetNameFrom(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Create string was null or empty.");

            var parameters = DatabaseFromStringFactory.GetParameters(createString);

            var fileName = parameters.FirstOrDefault(p => p.Key == "filename").Value ?? string.Empty;

            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static string GetFileNameFrom(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Create string was null or empty.");

            var parameters = DatabaseFromStringFactory.GetParameters(createString);

            var fileName = parameters.FirstOrDefault(p => p.Key == "property").Value;

            return Path.Combine(Path.GetFileNameWithoutExtension(fileName), ".index");
        }
    }
}
