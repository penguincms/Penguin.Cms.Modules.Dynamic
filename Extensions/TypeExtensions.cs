using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using System;

namespace Penguin.Cms.Modules.Dynamic.Extensions
{
    public static class TypeExtensions
    {
        private const string DYNAMIC_PROXIES_NS = "System.Data.Entity.DynamicProxies";

        public static string? GetUnproxifiedName(this Type t)
        {
            return t is null
                ? throw new ArgumentNullException(nameof(t))
                : t.Namespace == DYNAMIC_PROXIES_NS ? t.BaseType!.FullName : t.FullName;
        }

        public static string GetUnproxifiedName(this IMetaType t)
        {
            return t is null ? throw new ArgumentNullException(nameof(t)) : t.Namespace == DYNAMIC_PROXIES_NS ? t.BaseType.FullName : t.FullName;
        }
    }
}