/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Tests.Mocks
{
    [Serializable]
    public class MockClassA : Object
    {
        public int Id { get; set; }
        public virtual string Name { get; set; }

        public static int GetId(MockClassA entity)
        {
            return entity == null ? 0 : entity.Id;
        }

        public static void SetId(MockClassA entity, int id)
        {
            if (entity == null)
                return;

            entity.Id = id;
        }

        public static string GetCatalogId(MockClassA entity)
        {
            return (entity == null || entity.Name == null || entity.Name.Length < 1 ? "_" : entity.Name.Substring(0, 1).ToUpper());
        }

        public static string GetName(MockClassA entity)
        {
            return entity == null ? default(string) : entity.Name;
        }

        public static void SetName(MockClassA entity, string name)
        {
            if (entity == null)
                return;

            entity.Name= name;
        }

        public static string GetCatalogNull(MockClassA entity)
        {
            return null;
        }

    }

    internal static class Extend
    {
        public static MockClassA WithName(this MockClassA mock, string name)
        {
            mock.Name = name;

            return mock;
        }
    }
}
