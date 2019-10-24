using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using System;

namespace Penguin.Cms.Modules.Dynamic.Extensions
{
    public static class TypeExtensions
    {
        private const string DYNAMIC_PROXIES_NS = "System.Data.Entity.DynamicProxies";

        public static string GetUnproxifiedName(this Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (t.Namespace == DYNAMIC_PROXIES_NS)
            {
                return t.BaseType!.FullName!;
            }
            else
            {
                return t.FullName!;
            }
        }

        public static string GetUnproxifiedName(this IMetaType t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (t.Namespace == DYNAMIC_PROXIES_NS)
            {
                return t.BaseType.FullName;
            }
            else
            {
                return t.FullName;
            }
        }
    }
}