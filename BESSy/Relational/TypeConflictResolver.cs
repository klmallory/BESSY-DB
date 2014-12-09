using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;

namespace BESSy.Relational
{
    internal class TypeConflictResolver
    {
        private AppDomain _domain;
        private Dictionary<string, TypeBuilder> _builders = new Dictionary<string, TypeBuilder>();

        public void Bind(AppDomain domain)
        {
            domain.TypeResolve += Domain_TypeResolve;
        }

        public void Release()
        {
            if (_domain != null)
            {
                _domain.TypeResolve -= Domain_TypeResolve;
                _domain = null;
            }
        }

        public void AddTypeBuilder(TypeBuilder builder)
        {
            _builders.Add(builder.Name, builder);
        }

        Assembly Domain_TypeResolve(object sender, ResolveEventArgs args)
        {
            if (_builders.ContainsKey(args.Name))
            {
                return _builders[args.Name].CreateType().Assembly;
            }
            else
            {
                return null;
            }
        }
    }
}
