using Penguin.Persistence.Abstractions.Attributes.Control;
using System;

namespace Penguin.Cms.Modules.Dynamic.Attributes
{
    /// <summary>
    /// An attribute intended for use on controller actions that tells the dynamic system to use the target method
    /// to render the object in the provided display context
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class DynamicPropertyHandlerAttribute : Attribute
    {
        /// <summary>
        /// The display context the action is used in
        /// </summary>
        public DisplayContexts DisplayContexts { get; set; }

        /// <summary>
        /// The name of the property of the meta object that this action is an editor for
        /// </summary>
        public string PropertyName { get; internal set; }

        /// <summary>
        /// The target object type handled by this action
        /// </summary>
        public Type Type { get; internal set; }

        /// <summary>
        /// Creates a new instance of this attribute
        /// </summary>
        /// <param name="context">The display context the action is used in</param>
        /// <param name="type">The name of the property of the meta object that this action is an editor for</param>
        /// <param name="propertyName">The target object type handled by this action</param>
        public DynamicPropertyHandlerAttribute(DisplayContexts context, Type type, string propertyName)
        {
            DisplayContexts = context;
            Type = type;
            PropertyName = propertyName;
        }

        public DisplayContexts Context { get; }
    }
}