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
using System.Text.RegularExpressions;
using BESSy.Json;
using BESSy.Json.Linq;

namespace BESSy.Extensions
{
    public static class JObjectExtensions
    {
        static string IndexRegEx = @"[\d+]";

        public static JObject Add<ValueType>(this JObject jobject, string name, ValueType value)
        {
            jobject.Add(new JProperty(name, value));

            return jobject;
        }

        public static T[] GetAsTypedArray<T>(this JObject token, string property)
        {
            return GetAsTypedArray<T>((JToken)token, property);
        }

        public static T[] GetAsTypedArray<T>(this JToken token, string property)
        {
            var result = new T[0];

            var selected = token.SelectToken(property);

            if (selected == null || !selected.HasValues)
                return result;

            return GetTypeArrayFrom<T>(selected);
        }

        public static T[] GetTypeArrayFrom<T>(JToken token)
        {
            var values = token["$values"];

            if (values == null)
                values = token;

            if (!values.HasValues)
                return new T[0];

            return values.Children().Select(s => s.ToObject<T>()).ToArray();
        }

        public static void SetValue<T>(this JObject obj, string token, T value, JsonSerializer serializer)
        {

            if (String.IsNullOrWhiteSpace(token))
                return;

            JToken sVal = null;

            sVal = JToken.FromObject(value, serializer);

            var eVal = obj.SelectToken(token) as JValue;

            if (eVal == null && sVal == null)
                return;

            if (eVal == null && sVal != null)
            {
                string current = string.Empty;
                var cTok = obj.Root;

                var count = 0;
                var layers = token.Split('.');

                foreach (var s in layers)
                {
                    var eProp = obj.SelectToken(current + s) as JObject;

                    if (eProp == null)
                    {
                        var missing = string.IsNullOrWhiteSpace(current) ? token : token.Replace(current, "");

                        if (!missing.Contains(".") && !Regex.Match(missing, IndexRegEx).Success)
                        {
                            ((JObject)obj.SelectToken(current.TrimEnd('.'))).Add(s, sVal);
                            return;
                        }
                        else if (!missing.Contains(".") && Regex.Match(missing, IndexRegEx).Success)
                        {
                            ((JObject)obj.SelectToken(current.TrimEnd('.'))).Add(s, new JArray(sVal));
                            return;
                        }
                        else
                        {
                            var all = missing.Split('.');
                            var first = all.First();
                            var tok = JToken.FromObject(value, serializer);

                            foreach (var prop in all.Reverse().Except(new string[] { first }))
                            {
                                if (!Regex.Match(missing, IndexRegEx).Success)
                                    tok = JProperty.Parse(@"{""" + prop + @""": " + tok.ToString() + @"}");
                                else
                                    tok = JArray.Parse("[" + tok.ToString() + "]");
                            }

                            obj.Add(first, tok);
                        }

                        break;
                    }
                    else
                        current += s + ".";

                    count++;
                }

                eVal = obj.SelectToken(token) as JValue;
            }

            if (eVal != null && sVal == null)
                eVal.Replace(JToken.Parse(@"{}"));
            else
                eVal.Replace(sVal);
        }
    }
}