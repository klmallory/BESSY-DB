using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using BESSy.Extensions;
using System.IO;
using System.Runtime;

namespace BESSy.Factories
{
    public static class JsonWriterFactory
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public static JsonWriter CreateFrom(StreamWriter streamWriter, JsonSerializerSettings settings)
        {
            var jw = new JsonTextWriter(streamWriter);

            // reader/writer specific
            // unset values won't override reader/writer set values
            jw.Formatting = settings.Formatting;
            jw.DateFormatHandling = settings.DateFormatHandling;
            jw.DateTimeZoneHandling = settings.DateTimeZoneHandling;
            jw.DateFormatString = settings.DateFormatString;
            jw.FloatFormatHandling = settings.FloatFormatHandling;
            jw.StringEscapeHandling = settings.StringEscapeHandling;
            jw.Culture = settings.Culture;

            return jw;
        }
    }
}
