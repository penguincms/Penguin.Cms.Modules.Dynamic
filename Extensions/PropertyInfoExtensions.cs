using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Penguin.Cms.Modules.Dynamic.Extensions
{
    public static class PropertyInfoExtensions
    {
        public static string GetJsonName(this PropertyInfo propertyInfo)
        {
            string jPropName = propertyInfo.Name;

            if (propertyInfo.GetCustomAttribute<JsonPropertyAttribute>() is JsonPropertyAttribute jp)
            {
                jPropName = jp.PropertyName ?? jPropName;
            }

            return jPropName;
        }
    }
}