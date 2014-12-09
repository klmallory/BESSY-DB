using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json.Bson;
using System.IO;
using BESSy.Json;
using System.Runtime;

namespace BESSy.Factories
{
    public static class BsonWriterFactory
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public static BsonWriter CreateFrom(Stream stream, JsonSerializerSettings settings)
        {
            var bw = new BsonWriter(stream);

            // reader/writer specific
            // unset values won't override reader/writer set values
            bw.Formatting = settings.Formatting;
            bw.DateFormatHandling = settings.DateFormatHandling;
            bw.DateTimeZoneHandling = settings.DateTimeZoneHandling;
            bw.DateFormatString = settings.DateFormatString;
            bw.FloatFormatHandling = settings.FloatFormatHandling;
            bw.StringEscapeHandling = settings.StringEscapeHandling;
            bw.Culture = settings.Culture;

            return bw;
        }
    }
}
