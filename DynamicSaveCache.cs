using Penguin.Persistence.Abstractions;
using System;
using System.Collections.Generic;

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

            Type oType = this.GetCacheType(keyedObject);

            if (!this.cache.TryGetValue(oType, out Dictionary<int, KeyedObject> store))
            {
                store = new Dictionary<int, KeyedObject>();
                this.cache.Add(oType, store);
            }

            store.Add(keyedObject._Id, keyedObject);
        }

        public KeyedObject GetObject(int Id, Type type)
        {
            if (this.cache.TryGetValue(type, out Dictionary<int, KeyedObject> typeDict) && typeDict.TryGetValue(Id, out KeyedObject o))
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

            return this.GetObject(toGet._Id, this.GetCacheType(toGet)) as T;
        }

        public void TryAddObject<T>(T keyedObject) where T : KeyedObject
        {
            if (keyedObject is null)
            {
                throw new ArgumentNullException(nameof(keyedObject));
            }

            KeyedObject old = this.GetObject(keyedObject);

            if (old is null)
            {
                this.AddObject(keyedObject);
            }
            else
            {
                if (old != keyedObject)
                {
                    throw new Exception($"Cache already contains object of type {this.GetCacheType(keyedObject)} with Key {keyedObject._Id} however the version passed in appears to be a different instance");
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