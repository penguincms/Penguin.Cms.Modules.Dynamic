using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Penguin.Cms.Modules.Dynamic.Extensions
{
    public static class JObjectExtensions
    {
        public static TReturn Property<TReturn>(this JObject source, PropertyInfo propertyInfo) where TReturn : class
        {
            return source.Property<TReturn>(propertyInfo, false);
        }

        public static JProperty Property(this JObject source, PropertyInfo propertyInfo)
        {
            return source.Property(propertyInfo.GetJsonName());
        }

        public static TReturn Remove<TReturn>(this JObject source, PropertyInfo propertyInfo) where TReturn : class
        {
            return source.Property<TReturn>(propertyInfo, true);
        }

        private static TReturn Property<TReturn>(this JObject source, PropertyInfo propertyInfo, bool Remove) where TReturn : class
        {
            return source.Property<TReturn>(propertyInfo.GetJsonName(), propertyInfo.PropertyType, Remove);
        }

        private static TReturn Property<TReturn>(this JObject source, string propertyName, Type propertyType, bool Remove) where TReturn : class
        {
            JToken jEntity = source.Property(propertyName).Value as JToken;

            if (Remove)
            {
                source.Remove(propertyName);
            }

            TReturn newValue = null;

            if (!(jEntity is null))
            {
                newValue = JsonConvert.DeserializeObject(jEntity.ToString(), propertyType) as TReturn;
            }

            return newValue;
        }
    }
}