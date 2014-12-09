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
using System.Resources;
using System.Diagnostics;
using System.IO;

namespace BESSy.Resources
{
    public class ResourceWrapper<EntityType> : AbstractResourceWrapper<EntityType>
    {
        public ResourceWrapper(Func<byte[], EntityType> readByteDelegate, Func<Stream, EntityType> readStreamDelegate,  Func<string, EntityType> readStringDelegate,  ResourceManager resources) : base(resources)
        {
            _readByteDelegate = readByteDelegate;
            _readStreamDelegate = readStreamDelegate;
            _readStringDelegate = readStringDelegate;
        }

        Func<byte[], EntityType> _readByteDelegate;
        Func<Stream, EntityType> _readStreamDelegate;
        Func<string, EntityType> _readStringDelegate;

        public override EntityType GetFrom(string value)
        {
            try
            {
                return _readStringDelegate.Invoke(value);
            }
            catch (Exception ex) { Trace.TraceError(ex.ToString()); }

            return default(EntityType);
        }

        public override EntityType GetFrom(System.IO.Stream contents)
        {
            try
            {
                return _readStreamDelegate.Invoke(contents);
            }
            catch (Exception ex) { Trace.TraceError(ex.ToString()); }

            return default(EntityType);
        }

        public override EntityType GetFrom(byte[] contents)
        {
            try
            {
                return _readByteDelegate.Invoke(contents);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }

            return default(EntityType);
        }

        public override EntityType Fetch(string id)
        {
            return GetFileContents(id);
        }


    }
}
