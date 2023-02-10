using Penguin.Persistence.Abstractions;
using System;
using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic
{
    public class DynamicSaveCache
    {
        private readonly Dictionary<Type, Dictionary<int, KeyedObject>> cache = new();

        public void AddObject<T>(T keyedObject) where T : KeyedObject
        {
            if (keyedObject is null)
            {
                throw new ArgumentNullException(nameof(keyedObject));
            }

            Type oType = GetCacheType(keyedObject);

            if (!cache.TryGetValue(oType, out Dictionary<int, KeyedObject> store))
            {
                store = new Dictionary<int, KeyedObject>();
                cache.Add(oType, store);
            }

            store.Add(keyedObject._Id, keyedObject);
        }

        public KeyedObject? GetObject(int Id, Type type)
        {
            return cache.TryGetValue(type, out Dictionary<int, KeyedObject> typeDict) && typeDict.TryGetValue(Id, out KeyedObject o) ? o : null;
        }

        public T? GetObject<T>(T toGet) where T : KeyedObject
        {
            return toGet is null ? throw new ArgumentNullException(nameof(toGet)) : GetObject(toGet._Id, GetCacheType(toGet)) as T;
        }

        public void TryAddObject<T>(T keyedObject) where T : KeyedObject
        {
            if (keyedObject is null)
            {
                throw new ArgumentNullException(nameof(keyedObject));
            }

            KeyedObject old = GetObject(keyedObject);

            if (old is null)
            {
                AddObject(keyedObject);
            }
            else
            {
                if (old != keyedObject)
                {
                    throw new Exception($"Cache already contains object of type {GetCacheType(keyedObject)} with Key {keyedObject._Id} however the version passed in appears to be a different instance");
                }
            }
        }

        private static Type GetCacheType(KeyedObject toCheck)
        {
            if (toCheck is null)
            {
                throw new ArgumentNullException(nameof(toCheck));
            }

            Type toReturn = toCheck.GetType();

            while (toReturn.Name.StartsWith(toReturn.BaseType.Name + "_"))
            {
                toReturn = toReturn.BaseType;
            }

            return toReturn;
        }
    }
}