using Penguin.Persistence.Abstractions.Attributes.Control;
using System;

namespace Penguin.Cms.Modules.Dynamic.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DynamicHandlerAttribute : Attribute
    {
        public DisplayContexts DisplayContexts { get; set; }
        public Type[] ToHandle { get; set; }

        public DynamicHandlerAttribute(DisplayContexts context, params Type[] toHandle)
        {
            if (toHandle.Length == 0)
            {
                throw new ArgumentException(nameof(toHandle), "Must specify at least one type to handle");
            }

            this.DisplayContexts = context;

            this.ToHandle = toHandle;
        }
    }
}