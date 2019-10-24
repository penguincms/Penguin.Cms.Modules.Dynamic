using Newtonsoft.Json;
using Penguin.Extensions.Collections;
using Penguin.Reflection.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Penguin.Cms.Modules.Dynamic.Rendering
{
    public class LazyLoadForce : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsCollection();
        }

        [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters")]
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (objectType is null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }

            //If its a collection we're going to look through it just to make sure its loaded
            //Thats it. This should only touch collections that are referenced by the source
            //json as a target

            if (existingValue != null)
            {
                foreach (object? o in (existingValue as IEnumerable).ToGenericList())
                {
                }
            }

            if (serializer is null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (Activator.CreateInstance(typeof(List<>).MakeGenericType(objectType.GetCollectionType())) is IList toReturn)
            {
                serializer.Populate(reader, toReturn);

                return toReturn;
            }
            else
            {
                throw new Exception("What the fuck?");
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }
    }
}