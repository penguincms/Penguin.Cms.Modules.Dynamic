using Penguin.Persistence.Abstractions;
using Penguin.Persistence.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Cms.Modules.Dynamic
{
    public class DynamicSaveCache
    {
        private readonly Dictionary<Type, Dictionary<int, KeyedObject>> cache = new Dictionary<Type, Dictionary<int, KeyedObject>>();

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

        public KeyedObject GetObject(int Id, Type type)
        {
            if (cache.TryGetValue(type, out Dictionary<int, KeyedObject> typeDict) && typeDict.TryGetValue(Id, out KeyedObject o))
            {
                return o;
            }
            else
            {
                return null;
            }
        }

        public T GetObject<T>(T toGet) where T : KeyedObject
        {
            if (toGet is null)
            {
                throw new ArgumentNullException(nameof(toGet));
            }

            return GetObject(toGet._Id, GetCacheType(toGet)) as T;
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

        private Type GetCacheType(KeyedObject toCheck)
        {
            Type toReturn = toCheck.GetType();

            while (toReturn.Name.StartsWith(toReturn.BaseType.Name + "_"))
            {
                toReturn = toReturn.BaseType;
            }

            return toReturn;
        }
    }
}