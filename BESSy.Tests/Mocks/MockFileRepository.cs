using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using BESSy.Files;

namespace BESSy.Tests.Mocks
{
    [XmlInclude(typeof(XmlContainer<MockClassC>))]
    [XmlInclude(typeof(MockClassC))]
    public class MockContainer : XmlContainer<MockClassC>
    {
        public override List<MockClassC> AsList { get; set; }
    }

    public class MockFileRepository : AbstractFileRepository<MockContainer, MockClassC, int>
    {
        public MockFileRepository(string fileName, string path)
            : base(fileName, new XmlFileManager<MockContainer>(path))
        {

        }

        protected override int GetId(MockClassC item)
        {
            return item.Id;
        }
    }
}
