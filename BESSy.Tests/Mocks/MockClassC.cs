/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Security.Permissions;

namespace BESSy.Tests.Mocks
{
    public class MockClassC : MockClassB
    {
        public short LittleId;
        public long BigId { get; set; }
        public ushort Unsigned16 { get; set; }
        public uint Unsigned32 { get; set; }
        public ulong Unsigned64 { get; set; }
        public string ReferenceCode { get; set; }
        public MockStruct Location { get; set; }
        IDictionary<int, MockClassD> Ds { get; set; }
        public virtual MockClassC Friend { get; set; }
        public virtual MockDomain Other { get; set; }

        public static void Validate(MockClassC item, MockClassC orig)
        {
            Assert.AreEqual(item.Id, orig.Id);
            Assert.AreEqual(item.Name, orig.Name);
            Assert.AreEqual(item.GetSomeCheckSum[0], orig.GetSomeCheckSum[0]);
            Assert.AreEqual(item.Location.X, orig.Location.X);
            Assert.AreEqual(item.Location.Y, orig.Location.Y);
            Assert.AreEqual(item.Location.Z, orig.Location.Z);
            Assert.AreEqual(item.Location.W, orig.Location.W);
            Assert.AreEqual(item.ReferenceCode, orig.ReferenceCode);
            Assert.AreEqual(item.ReplicationID, orig.ReplicationID);
            Assert.AreEqual(item.CatalogName, orig.CatalogName);
            Assert.AreEqual(item.CatalogNameNull, orig.CatalogNameNull);
            Assert.AreEqual(item.DecAnimal, orig.DecAnimal);
            Assert.AreEqual(item.MyDate, orig.MyDate);
            Assert.AreEqual(item.BigId, orig.BigId);
            Assert.AreEqual(item.Unsigned16, orig.Unsigned16);
            Assert.AreEqual(item.Unsigned32, orig.Unsigned32);
            Assert.AreEqual(item.Unsigned64, orig.Unsigned64);
            Assert.AreEqual(item.LittleId, orig.LittleId);

            if (item.Friend != null)
                Validate(item.Friend, orig.Friend);

            if (item.Other != null)
                Validate(item.Other, orig.Other);
        }
    }
}
