using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using NUnit.Framework;

namespace BESSy.Tests.Mocks
{
    public static class Validation
    {
        internal static bool ValidateA(MockClassA a, MockClassA b)
        {
            Assert.AreEqual(a.CatalogName, b.CatalogName);
            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.ReplicationID, b.ReplicationID);

            return true;
        }

        internal static bool ValidateB(MockClassB a, MockClassB b)
        {
            if (a == null && b == null)
                return true;

           ValidateA(a as MockClassA, b);

            Assert.AreEqual(a.MyDate, b.MyDate);
            Assert.AreEqual(a.DecAnimal, b.DecAnimal);

            return true;
        }

        internal static bool ValidateC(MockClassC a, MockClassC b)
        {
            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.GetSomeCheckSum[0], b.GetSomeCheckSum[0]);
            Assert.AreEqual(a.Location.X, b.Location.X);
            Assert.AreEqual(a.Location.Y, b.Location.Y);
            Assert.AreEqual(a.Location.Z, b.Location.Z);
            Assert.AreEqual(a.Location.W, b.Location.W);
            Assert.AreEqual(a.ReferenceCode, b.ReferenceCode);
            Assert.AreEqual(a.ReplicationID, b.ReplicationID);
            Assert.AreEqual(a.CatalogName, b.CatalogName);
            Assert.AreEqual(a.CatalogNameNull, b.CatalogNameNull);
            Assert.AreEqual(a.DecAnimal, b.DecAnimal);
            Assert.AreEqual(a.MyDate, b.MyDate);
            Assert.AreEqual(a.BigId, b.BigId);
            Assert.AreEqual(a.Unsigned16, b.Unsigned16);
            Assert.AreEqual(a.Unsigned32, b.Unsigned32);
            Assert.AreEqual(a.Unsigned64, b.Unsigned64);
            Assert.AreEqual(a.LittleId, b.LittleId);

            if (a.Friend != null)
                ValidateC(a.Friend, b.Friend);

            return true;
        }

        internal static bool ValidateDomain(dynamic a, MockDomain b)
        {
            if (a == null && b == null)
                return true;

            ValidateC(a as MockClassC, b);

            if (a.ADomain == null && b.ADomain == null)
                return true;

            ValidateA(a.ADomain, b.ADomain);

            if (a.BDomain == null && b.BDomain == null)
                return true;

            ValidateB(a.BDomain, b.BDomain);

            if (a.CDomain == null && b.CDomain == null)
                return true;

            ValidateC(a.CDomain, b.CDomain);

            if (a.CDomains == null && b.CDomains == null)
                return true;

            Assert.AreEqual(a.CDomains.Count, b.CDomains.Count);
            for (var i = 0; i < a.CDomains.Count; i++)
                ValidateC(a.CDomains[i], b.CDomains[i]);

            if (a.BDomains == null && b.BDomains == null)
                return true;

            Assert.AreEqual(a.BDomains.Length, b.BDomains.Length);
            for (var i = 0; i < a.BDomains.Length; i++)
                ValidateB(a.BDomains[i], b.BDomains[i]);

            if (a.MyHashMash != null)
            {
                Assert.IsNotNull(b.MyHashMash);
                Assert.AreEqual(a.MyHashMash.Count, b.MyHashMash.Count);

                foreach (var k in a.MyHashMash.Keys)
                {
                    Assert.IsTrue(b.MyHashMash.ContainsKey(k));
                    Assert.AreEqual(a.MyHashMash[k], b.MyHashMash[k]);
                }
            }

            Assert.AreEqual(a.GetFieldTestValue(), b.GetFieldTestValue());
            Assert.AreEqual(a.GetFieldTest2Value(), b.GetFieldTest2Value());

            return true;
        }
    }
}
