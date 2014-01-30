using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace BESSy.Factories
{
    public class DatabaseFactoryException : ApplicationException
    {
        public DatabaseFactoryException(string message)
            : base(message)
        {

        }
    }

    public static class DatabaseFactory
    {
        static string BAD_CONNECTION_STRING = "Connection string format error: ";
        static string ID_TYPE_GENERIC = "idtype parameter can not be a generic type.";

        static IList<string> paramNames = new List<string>()
        {
            "filename",
            "idtype",
            "idtoken",
            "entitytype",
            "formattertype",
            "seedtype",
            "binconvertertype",
            "transactionmanagertype",
            "filefactorytype",
            "cachefactorytype",
            "indexfactorytype",
            "indexfilefactorytype",
            "rowsynchronizertype",
            "hashtoken",
            "keytoken"
        };

        static Type GetType(string[] typeNames)
        {
            var types = new List<Type>();

            foreach (var typeName in typeNames)
            {
                if (typeName.Contains(","))
                {
                    var innerTypes = typeName.Split(new string[] { "," }, StringSplitOptions.None);

                    types.Add(GetType(innerTypes));
                }
                else if (typeName.Contains("|"))
                {
                    var innerTypes = typeName.Split(new string[] { "|" }, StringSplitOptions.None);

                    types.Add(GetType(innerTypes));
                }
                else
                {
                    types.Add(Type.GetType(typeName));
                }
            }

            if (types.Count > 1)
                return types[0].MakeGenericType(types.Skip(1).ToArray());
            else
                return types[0];
        }

        public static object Create(string createString)
        {
            if (string.IsNullOrWhiteSpace(createString))
                throw new ArgumentNullException("Connection string was null or empty.");

            IDictionary<string, object> parameters = new Dictionary<string, object>();

            var regEx = new Regex(@"(\w*=)(.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var match = regEx.Match(createString);
            Match m = match.NextMatch();

            while (m != null)
            {
                if (!m.Success
                    || m.Groups.Count != 2
                    || string.IsNullOrWhiteSpace(m.Groups[0].Value)
                    || string.IsNullOrWhiteSpace(m.Groups[1].Value))
                {
                    Trace.TraceError("Could not parse argument {0}", m.Groups.Count > 0 ? m.Groups[0].Value : null);
                    continue;
                }

                switch (m.Value.Trim().ToLower())
                {
                    case "filename":
                        parameters.Add("filename", m.Groups[1].Value.Trim());
                        break;
                    case "idtype":
                        var type = Type.GetType(m.Groups[1].Value.Trim());

                        if (type.IsGenericType)
                            throw new DatabaseFactoryException(BAD_CONNECTION_STRING + ID_TYPE_GENERIC);

                        parameters.Add("idtype", type);
                        break;
                    case "idtoken":
                        parameters.Add("idtoken", m.Groups[1].Value.Trim());
                        break;
                    case "entitytype":
                        var typeNames = m.Groups[1].Value.Trim().Split(new string[] { ";" }, StringSplitOptions.None);
                        parameters.Add("entitytype", GetType(typeNames));

                        break;
                }

                m = match.NextMatch();
            }

            throw new NotImplementedException();
        }

    }
}
