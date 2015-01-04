using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Serialization;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using BESSy.Extensions;

namespace BESSy.Tests.ExtensionsTest
{
    [TestFixture]
    public class JObjectExtensionTests : FileTest
    {

        [Test]
        public void JObjectSetsValue()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();


            var serializer = JsonSerializer.Create(BSONFormatter.GetDefaultSettings());

            var c = TestResourceFactory.CreateRandom() as MockClassC;

            c.Location = default(MockStruct);
            c.Friend = null;

            var obj = JObject.FromObject(c, serializer);

            obj.SetValue<float>("Location.X", 1.11f, serializer);

            Assert.AreEqual(1.11f, obj.SelectToken("Location.X").ToObject<float>());

            obj.SetValue<float>("Friend.Location.X", 2.22f, serializer);

            Assert.AreEqual(2.22f, obj.SelectToken("Friend.Location.X").ToObject<float>());
            
        }
    }
}
