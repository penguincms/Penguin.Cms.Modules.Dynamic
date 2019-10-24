using Penguin.Persistence.Abstractions.Attributes.Control;
using System;

namespace Penguin.Cms.Modules.Dynamic.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DynamicPropertyHandlerAttribute : Attribute
    {
        public DisplayContexts DisplayContexts { get; set; }
        public string PropertyName { get; set; }
        public Type Type { get; set; }

        public DynamicPropertyHandlerAttribute(DisplayContexts context, Type type, string propertyName)
        {
            DisplayContexts = context;
            Type = type;
            PropertyName = propertyName;
        }
    }
}