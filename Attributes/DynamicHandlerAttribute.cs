using Penguin.Persistence.Abstractions.Attributes.Control;
using System;

namespace Penguin.Cms.Modules.Dynamic.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DynamicHandlerAttribute : Attribute
    {
        public DisplayContexts DisplayContexts { get; set; }

        public Type[] ToHandle { get; internal set; }

        public DynamicHandlerAttribute(DisplayContexts context, params Type[] toHandle)
        {
            if (toHandle is null || toHandle.Length == 0)
            {
                throw new ArgumentException("Must specify at least one type to handle", nameof(toHandle));
            }

            DisplayContexts = context;

            ToHandle = toHandle;
        }

        public DisplayContexts Context { get; }
    }
}