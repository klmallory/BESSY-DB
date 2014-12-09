/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using System.IO;
using BESSy.Files;
using BESSy.Json.Converters;

namespace BESSy.Containers
{
    public abstract class ResourceContainer
    {
        public ResourceContainer()
        {
            _catagory = this.GetType().FullName;
        }

        private readonly string _catagory;

        public virtual string CatalogName { get { return _catagory; } }
        public virtual int CatalogSize { get { return 0; } }

        public virtual string Name { get; set; }


        [JsonProperty()]
        protected byte[] _bytes { get; set; }
        protected object resourceCache;

        protected abstract object GetResourceFrom(byte[] bytes);
        protected abstract byte[] GetBytesFrom(object resource);
        public virtual string ResourceType { get; protected set; }

        public virtual T GetResource<T>()
        {
            if (resourceCache != null)
                return (T)resourceCache;

            resourceCache = GetResourceFrom(_bytes);

            return (T)resourceCache;
        }

        public virtual void SetResource<T>(T resource)
        {
            resourceCache = resource;
            _bytes = GetBytesFrom(resource);
        }
    }
}
