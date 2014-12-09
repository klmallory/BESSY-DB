using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json.Linq;
using BESSy.Serialization.Converters;
using System.Text.RegularExpressions;

namespace BESSy.Queries
{
    public interface IExpressionResolver<IdType, EntityType>
    {
        System.Collections.Generic.IList<JObject> ExecuteSelect(WhereExpression select);
    }

    public class ExpressionResolver<IdType, EntityType> : IExpressionResolver<IdType, EntityType>
    {
        public ExpressionResolver(ITransactionalDatabase<IdType, EntityType> database)
        {
            _database = database;
        }

        BinConverterGuid _guidConverter = new BinConverterGuid();

        ITransactionalDatabase<IdType, EntityType> _database;

        static string IndexRegEx = @"[\d+]";

        protected virtual bool CompareTo(CompareEnum op, JToken dbToken, object qVal, ValueEnum valueType)
        {
            try
            {
                var jVal = dbToken as JValue;

                if (valueType == ValueEnum.Null)
                {
                    switch (op)
                    {
                        case CompareEnum.Equals:
                            {
                                if (jVal == null || jVal.Value == null)
                                    return true;

                                return false;
                            }
                        case CompareEnum.NotEquals:
                            return jVal != null && jVal.Value != null;
                        default:
                            return false;
                    }
                }
                else if (jVal == null || jVal.Value == null)
                    return false;

                switch (valueType)
                {
                    case ValueEnum.Byte:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (byte)jVal == (byte)qVal;
                                case CompareEnum.NotEquals:
                                    return (byte)jVal != (byte)qVal;
                                case CompareEnum.Greater:
                                    return (byte)qVal > (byte)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (byte)qVal >= (byte)jVal;
                                case CompareEnum.Lesser:
                                    return (byte)qVal < (byte)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (byte)qVal <= (byte)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Int:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (int)jVal == (int)qVal;
                                case CompareEnum.NotEquals:
                                    return (int)jVal != (int)qVal;
                                case CompareEnum.Greater:
                                    return (int)qVal > (int)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (int)qVal >= (int)jVal;
                                case CompareEnum.Lesser:
                                    return (int)qVal < (int)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (int)qVal <= (int)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Long:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (long)jVal == (long)qVal;
                                case CompareEnum.NotEquals:
                                    return (long)jVal != (long)qVal;
                                case CompareEnum.Greater:
                                    return (long)qVal > (long)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (long)qVal >= (long)jVal;
                                case CompareEnum.Lesser:
                                    return (long)qVal < (long)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (long)qVal <= (long)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.SmallInt:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (short)jVal == (short)qVal;
                                case CompareEnum.NotEquals:
                                    return (short)jVal != (short)qVal;
                                case CompareEnum.Greater:
                                    return (short)qVal > (short)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (short)qVal >= (short)jVal;
                                case CompareEnum.Lesser:
                                    return (short)qVal < (short)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (short)qVal <= (short)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.UInt:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (uint)jVal == (uint)qVal;
                                case CompareEnum.NotEquals:
                                    return (uint)jVal != (uint)qVal;
                                case CompareEnum.Greater:
                                    return (uint)qVal > (uint)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (uint)qVal >= (uint)jVal;
                                case CompareEnum.Lesser:
                                    return (uint)qVal < (uint)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (uint)qVal <= (uint)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.ULong:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (ulong)jVal == (ulong)qVal;
                                case CompareEnum.NotEquals:
                                    return (ulong)jVal != (ulong)qVal;
                                case CompareEnum.Greater:
                                    return (ulong)qVal > (ulong)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (ulong)qVal >= (ulong)jVal;
                                case CompareEnum.Lesser:
                                    return (ulong)qVal < (ulong)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (ulong)qVal <= (ulong)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.USmallInt:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (ushort)jVal == (ushort)qVal;
                                case CompareEnum.NotEquals:
                                    return (ushort)jVal != (ushort)qVal;
                                case CompareEnum.Greater:
                                    return (ushort)qVal > (ushort)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (ushort)qVal >= (ushort)jVal;
                                case CompareEnum.Lesser:
                                    return (ushort)qVal < (ushort)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (ushort)qVal <= (ushort)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Float:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (float)jVal == (float)qVal;
                                case CompareEnum.NotEquals:
                                    return (float)jVal != (float)qVal;
                                case CompareEnum.Greater:
                                    return (float)qVal > (float)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (float)qVal >= (float)jVal;
                                case CompareEnum.Lesser:
                                    return (float)qVal < (float)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (float)qVal <= (float)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Double:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (double)jVal == (double)qVal;
                                case CompareEnum.NotEquals:
                                    return (double)jVal != (double)qVal;
                                case CompareEnum.Greater:
                                    return (double)qVal > (double)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (double)qVal >= (double)jVal;
                                case CompareEnum.Lesser:
                                    return (double)qVal < (double)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (double)qVal <= (double)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Decimal:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (decimal)jVal == (decimal)qVal;
                                case CompareEnum.NotEquals:
                                    return (decimal)jVal != (decimal)qVal;
                                case CompareEnum.Greater:
                                    return (decimal)qVal > (decimal)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (decimal)qVal >= (decimal)jVal;
                                case CompareEnum.Lesser:
                                    return (decimal)qVal < (decimal)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (decimal)qVal <= (decimal)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Guid:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (Guid)jVal == (Guid)qVal;
                                case CompareEnum.NotEquals:
                                    return (Guid)jVal != (Guid)qVal;
                                case CompareEnum.Greater:
                                    return _guidConverter.Compare((Guid)qVal, (Guid)jVal) > 0;
                                case CompareEnum.GreaterOrEqual:
                                    return _guidConverter.Compare((Guid)qVal, (Guid)jVal) >= 0;
                                case CompareEnum.Lesser:
                                    return _guidConverter.Compare((Guid)qVal, (Guid)jVal) < 0;
                                case CompareEnum.LesserOrEqual:
                                    return _guidConverter.Compare((Guid)qVal, (Guid)jVal) <= 0;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.String:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (string)jVal == (string)qVal;
                                case CompareEnum.NotEquals:
                                    return (string)jVal != (string)qVal;
                                case CompareEnum.Greater:
                                    return string.Compare((string)qVal, (string)jVal) > 0;
                                case CompareEnum.GreaterOrEqual:
                                    return string.Compare((string)qVal, (string)jVal) >= 0;
                                case CompareEnum.Lesser:
                                    return string.Compare((string)qVal, (string)jVal) < 0;
                                case CompareEnum.LesserOrEqual:
                                    return string.Compare((string)qVal, (string)jVal) <= 0;
                                case CompareEnum.Like:
                                    {
                                        if (qVal == null)
                                            return false;

                                        return jVal.Value<string>().Contains((qVal as string));
                                    }

                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.DateTime:
                        {
                            switch (op)
                            {
                                case CompareEnum.Equals:
                                    return (DateTime)jVal == (DateTime)qVal;
                                case CompareEnum.NotEquals:
                                    return (DateTime)jVal != (DateTime)qVal;
                                case CompareEnum.Greater:
                                    return (DateTime)qVal > (DateTime)jVal;
                                case CompareEnum.GreaterOrEqual:
                                    return (DateTime)qVal >= (DateTime)jVal;
                                case CompareEnum.Lesser:
                                    return (DateTime)qVal < (DateTime)jVal;
                                case CompareEnum.LesserOrEqual:
                                    return (DateTime)qVal <= (DateTime)jVal;
                                default:
                                    return false;
                            }
                        }
                    case ValueEnum.Null:
                    default:
                        throw new QueryExecuteException(string.Format("Can not evaluate tBuilder of {0}, use only simple types in query tokens", qVal.GetType()));
                }
            }
            catch (Exception ex)
            {
                throw new QueryExecuteException(string.Format("Query values could not be evaluated {0}:{1} and {2}", dbToken.Path, dbToken, qVal), ex);
            }
        }

        public IList<JObject> ExecuteSelect(WhereExpression select)
        {
            if (select.First > 0)
            {
                return _database.SelectJObjFirst(delegate(JObject obj)
                {
                    foreach (var token in select.SelectTokens)
                    {
                        try
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                        catch (QueryExecuteException) { throw; }
                    }

                    return true;
                }, select.First);
            }
            else if (select.Last > 0)
            {
                return _database.SelectJObjLast(delegate(JObject obj)
                {
                    foreach (var token in select.SelectTokens)
                    {
                        try
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                        catch (QueryExecuteException) { throw; }
                    }

                    return true;
                }, select.Last);
            }
            else
            {
                return _database.SelectJObj(delegate(JObject obj)
                {
                    foreach (var token in select.SelectTokens)
                    {
                        try
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                        catch (QueryExecuteException) { throw; }
                    }

                    return true;
                });
            }
        }

        public IList<JObject> ExecuteScaler(ScalarSelectExpression select)
        {
            if (select.First > 0)
            {
                return _database.SelectScalarFirst(delegate(JObject obj)
                {
                    foreach (var token in select.SelectTokens)
                    {
                        try
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                        catch (QueryExecuteException) { throw; }
                    }

                    return true;
                }, select.First, select.Tokens).ToArray();
            }
            else if (select.Last > 0)
            {
                return _database.SelectScalarLast(delegate(JObject obj)
                {
                    foreach (var token in select.SelectTokens)
                    {
                        try
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                        catch (QueryExecuteException) { throw; }
                    }

                    return true;
                }, select.Last, select.Tokens).ToArray();
            }
            else
            {
                return _database.SelectScalar(delegate(JObject obj)
                {
                    try
                    {
                        foreach (var token in select.SelectTokens)
                        {

                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }
                    }
                    catch (QueryExecuteException) { throw; }

                    return true;
                }, select.Tokens).ToArray();
            }
        }

        public int ExecuteDelete(DeleteExpression delete)
        {
            try
            {
                if (delete.First > 0)
                {
                    return _database.DeleteFirst(delegate(JObject obj)
                    {
                        foreach (var token in delete.SelectTokens)
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }

                        return true;
                    }, delete.First);
                }
                else if (delete.Last > 0)
                {
                    return _database.DeleteLast(delegate(JObject obj)
                    {
                        foreach (var token in delete.SelectTokens)
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }

                        return true;
                    }, delete.Last);
                }
                else
                {
                    return _database.Delete(delegate(JObject obj)
                    {
                        foreach (var token in delete.SelectTokens)
                        {
                            if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                                return false;
                        }

                        return true;
                    });
                }
            }
            catch (QueryExecuteException) { throw; }
        }

        public int ExecuteUpdate(UpdateExpression update)
        {
            var type = Type.GetType(update.TypeName);

            return _database.UpdateScalar(type, delegate(JObject obj)
            {
                foreach (var token in update.Selector.SelectTokens)
                {
                    if (!CompareTo(token.CompareType, obj.SelectToken(token.SelectToken, false), token.Value, token.ValueType))
                        return false;
                }
                return true;
            }, new Action<JObject>(delegate(JObject entity)
            {
                if (entity == null)
                    return;

                foreach (var token in update.UpdateTokens)
                {
                    JToken sVal = null;

                    if (token.Value != null)
                        sVal = JToken.FromObject(token.Value, _database.Formatter.Serializer);

                    var eVal = entity.SelectToken(token.SetToken) as JValue;

                    if (eVal == null && sVal == null)
                        continue;

                    if (eVal == null && sVal != null)
                    {
                        string current = string.Empty;
                        var cTok = entity.Root;

                        var count = 0;
                        var layers = token.SetToken.Split('.');

                        foreach (var s in layers)
                        {
                            var eProp = entity.SelectToken(current + s) as JObject;

                            if (eProp == null)
                            {
                                var missing = string.IsNullOrWhiteSpace(current) ? token.SetToken : token.SetToken.Replace(current, "");
                                
                                if (!missing.Contains(".") && !Regex.Match(missing, IndexRegEx).Success)
                                    ((JObject)entity.SelectToken(current.TrimEnd('.'))).Add(s, sVal);
                                else if (!missing.Contains(".") && Regex.Match(missing, IndexRegEx).Success)
                                {
                                    ((JObject)entity.SelectToken(current.TrimEnd('.'))).Add(s, new JArray(sVal));
                                }
                                else
                                {
                                    var all = missing.Split('.');
                                    var first = all.First();
                                    var tok = JToken.FromObject(token.Value, _database.Formatter.Serializer);

                                    foreach (var prop in all.Reverse().Except(new string[] { first }))
                                    {
                                        if (!Regex.Match(missing, IndexRegEx).Success)
                                            tok = JProperty.Parse(@"{""" + prop + @""": " + tok.ToString() + @"}");
                                        else
                                            tok = JArray.Parse("[" + tok.ToString() + "]");
                                    }

                                    entity.Add(first, tok);
                                }

                                break;
                            }
                            else
                                current += s + ".";

                            count++;
                        }

                        eVal = entity.SelectToken(token.SetToken) as JValue;
                    }

                    if (eVal != null && sVal == null)
                        eVal.Replace(JToken.Parse(@"{}"));
                    else
                        eVal.Replace(sVal);
                }
            }
            ));
        }
    }
}
