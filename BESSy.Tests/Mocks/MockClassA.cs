/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Security.Permissions;

namespace BESSy.Tests.Mocks
{
    //[SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.ControlEvidence)]
    //[ReflectionPermission(SecurityAction.Assert, Flags = ReflectionPermissionFlag.MemberAccess, Unrestricted = true, MemberAccess = true)]
    public class MockClassA : Object
    {
        public int Id { get; set; }
        public Guid ReplicationID { get; set; }
        public virtual string Name { get; set; }
        public virtual string CatalogName { get { return Name == null || Name.Length < 1 ? "_" : Name.Substring(0, 1).ToUpper(); } }
        public virtual string CatalogNameNull { get { return null; } }
    }

    public static class Extend
    {
        public static MockClassA WithName(this MockClassA mock, string name)
        {
            mock.Name = name;

            return mock;
        }

        public static MockClassA WithId(this MockClassA mock, int id)
        {
            mock.Id = id;

            return mock;
        }
    }
}
