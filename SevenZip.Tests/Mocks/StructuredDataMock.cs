/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace SevenZip.Tests.Mocks
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=0)]
    public struct StructuredDataMock : ISerializable
    {
        public StructuredDataMock(SerializationInfo info, StreamingContext context) : this()
        {
            Index = info.GetInt32("Index");
            TypeId = info.GetInt32("TypeId");
        }

        public int Index { get; set; }
        public int TypeId { get; set; }

        public static readonly int SizeInBytes = Marshal.SizeOf(default(StructuredDataMock));

        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Index", Index);
            info.AddValue("TypeId", TypeId);
        }
    }
}
