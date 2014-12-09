using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using BESSy.Json;
using NUnit.Framework;

namespace BESSy.Tests.Mocks
{
    //[SecurityPermission(SecurityAction.Assert, Flags= SecurityPermissionFlag.ControlEvidence)]
    //[ReflectionPermission(SecurityAction.Assert, Flags = ReflectionPermissionFlag.MemberAccess, Unrestricted = true, MemberAccess = true, TypeInformation = true)]

    public class MockDomain : MockClassC
    {
        //[SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.ControlEvidence)]
        //[ReflectionPermission(SecurityAction.Assert, Flags = ReflectionPermissionFlag.MemberAccess, Unrestricted = true, MemberAccess = true, TypeInformation = true)]
        public MockDomain()
        {
            this.BDomains = new MockClassB[0];
            this.CDomains = new List<MockClassC>();
        }

        [JsonProperty("_fieldTest")]
        private int _fieldTest;

        public int GetFieldTestValue()
        {
            return _fieldTest;
        }

        public void SetFieldTestValue(int value)
        {
            _fieldTest = value;
        }

        public MockClassA ADomain;

        public virtual MockClassB BDomain { get; set; }

        public virtual MockClassC CDomain { get; set; }

        public virtual IList<MockClassC> CDomains { get; set; }
        public virtual MockClassB[] BDomains { get; set; }

        public virtual Dictionary<int, string> MyHashMash { get; set; }

        public MockDomain WithIds()
        {
            var id = 1;

            this.ADomain.Id = id++;
            this.BDomain.Id = id++;
            this.CDomain.Id = id++;

            if (this.CDomain.Friend != null)
                this.CDomain.Friend.Id = id++;
            if (this.CDomain.Other != null)
                this.CDomain.Other.Id = id++;

            this.BDomains.ToList().ForEach(b => b.Id = id++);
            this.CDomains.ToList().ForEach(delegate(MockClassC c) { c.Id = id++; if (c.Friend != null) { c.Friend.Id = CDomain.Id; } if (c.Other != null) { c.Other.Id = id++; } });

            return this;
        }
    }
}
