﻿using EFCore.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore
{
    public static class ExtensionsDbFunctionsExtensions
    {
        public static readonly MethodInfo ValueFromOpenJsonMethod = typeof(ExtensionsDbFunctionsExtensions)
            .GetMethod(nameof(ValueFromOpenJson));

        private static JsonType ToJsonType(this JTokenType type)
        {
            switch (type)
            {
                case JTokenType.Object:
                    return JsonType.Object;
                case JTokenType.Array:
                    return JsonType.Array;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return JsonType.Number;
                case JTokenType.String:
                    return JsonType.String;
                case JTokenType.Boolean:
                    return JsonType.Boolean;
                case JTokenType.Null:
                    return JsonType.Null;
                case JTokenType.Undefined:
                case JTokenType.Date:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.Comment:
                case JTokenType.Property:
                case JTokenType.Constructor:
                case JTokenType.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static T Convert<T>(this JToken token, JsonType type)
        {
            if (typeof(T) == typeof(string))
            {
                switch (type)
                {
                    case JsonType.Null:
                        return default(T);
                    case JsonType.String:
                    case JsonType.Number:
                    case JsonType.Boolean:
                        return token.Value<T>();
                    case JsonType.Object:
                    case JsonType.Array:
                        return (T)(object)token.ToString();
                    default:
                        throw new NotImplementedException();
                }
            }

            switch (type)
            {
                case JsonType.Null:
                    return default(T);
                case JsonType.String:
                case JsonType.Number:
                case JsonType.Boolean:
                    return token.Value<T>();
                case JsonType.Object:
                case JsonType.Array:
                    return default(T);
                default:
                    throw new NotImplementedException();
            }
        }

        public static IQueryable<JsonResult<T>> FromOpenJson<T>(this string json, string path = null)
        {
            return new[]
            {
                new JsonResult<T> { Value = (T)((object)"kind2") }
            }
            .AsQueryable()
            ;
        }

        public static IEnumerable<JsonResult<T>> ValueFromOpenJson<T>(this IQueryable<JsonResult<T>> self, string json, string path = null)
        {
            var lax = true;
            JToken token = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                token = JToken.Parse(json);
            }
            else
            {
                if (path.StartsWith("strict"))
                {
                    path = path.Substring("strict".Length + 1);
                    lax = false;
                }
                else if (path.StartsWith("lax"))
                {
                    // ignore
                    path = path.Substring("lax".Length + 1);
                }

                if (!path.StartsWith("$")) throw new ArgumentException(nameof(path));

                if (path == "$")
                {
                    token = JToken.Parse(json);
                }
                else
                {
                    var parts = path.Split('.');
                    if (parts.Length <= 1) throw new ArgumentException(nameof(path));
                    var root = JToken.Parse(json);
                    var first = parts[0];
                    if (first.Length > 1)
                    {
                        if (first.StartsWith("[") && first.EndsWith("]"))
                        {
                            if (!int.TryParse(first.Substring(1, first.Length - 2), out var ix))
                                throw new ArgumentException(nameof(path));
                            if (root is JArray ja)
                                root = ja[ix];
                            else throw new ArgumentOutOfRangeException(nameof(path));
                        }
                        else throw new ArgumentException(nameof(path));
                    }
                    token = parts.Skip(1).Aggregate(root, (current, next) =>
                    {
                        if (current == null)
                        {
                            if (lax) return null;
                            throw new ArgumentException(nameof(path));
                        }

                        var ix = -1;
                        if (next.EndsWith("]"))
                        {
                            var six = next.IndexOf("[");
                            if (six <= 0) throw new ArgumentException(nameof(path));
                            if (!int.TryParse(next.Substring(six + 1, next.Length - six - 2), out ix) || ix < 0)
                                throw new ArgumentException(nameof(path));
                        }
                        if (current is IDictionary<string, JToken> jobj)
                        {
                            if (!jobj.TryGetValue(next, out var prop))
                                current = prop;
                            else
                            {
                                if (lax) return null;
                                throw new ArgumentException(nameof(path));
                            }
                        }
                        if (ix >= 0)
                        {
                            if (current is JArray jarr && jarr.Count > ix)
                                current = jarr[ix];
                            else
                            {
                                if (lax) return null;
                                throw new ArgumentException(nameof(path));
                            }
                        }

                        //if (current is JArray ja)
                        return current;
                    });
                }
            }

            if (token.Type == JTokenType.Array)
            {
                return ((JArray)token).Select((item, i) =>
                {
                    var jtype = item.Type.ToJsonType();
                    return new JsonResult<T>
                    {
                        Key = $"{i}",
                        Type = jtype,
                        Value = item.Convert<T>(jtype)
                    };
                }).AsQueryable();
            }
            else if (token.Type == JTokenType.Object)
            {
                return ((IDictionary<string, JToken>)token).Select(prop =>
                {
                    var jtype = prop.Value.Type.ToJsonType();
                    return new JsonResult<T>
                    {
                        Key = prop.Key,
                        Type = jtype,
                        Value = prop.Value.Convert<T>(jtype)
                    };
                }).AsQueryable();
            }
            else throw new ArgumentException("invalid json", nameof(json));
        }
    }
}
