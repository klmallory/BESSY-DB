using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization;
using BESSy.Factories;

namespace BESSy.DataProvision
{
    public class DataProviderSettings
    {
        public DataProviderSettings(string workingDirectory)
        {
        }

        public DataProviderSettings(string workingDirectory, string formatterCreate)
        {
            FormatterCreate = formatterCreate;

            ConsolidatedFormatter = DatabaseFromStringFactory.CreateFormatter(formatterCreate);

            UsersMap = new Dictionary<string, Dictionary<string, string>>();
        }

        public string FormatterCreate { get; set; }
        public string WorkingDirectory { get; set; }
        public IQueryableFormatter ConsolidatedFormatter { get; set; }
        public Dictionary<string, Dictionary<string, string>> UsersMap { get; set; }
    }
}
