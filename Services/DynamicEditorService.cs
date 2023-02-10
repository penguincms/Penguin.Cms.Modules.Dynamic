using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Penguin.Cms.Abstractions.Attributes;
using Penguin.Cms.Modules.Dynamic.Attributes;
using Penguin.Cms.Modules.Dynamic.Rendering;
using Penguin.Cms.Web.Extensions;
using Penguin.DependencyInjection.Abstractions.Interfaces;
using Penguin.Extensions.String;
using Penguin.Persistence.Abstractions.Attributes.Control;
using Penguin.Reflection;
using Penguin.Reflection.Abstractions;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using Penguin.Reflection.Serialization.Extensions;
using Penguin.Reflection.Serialization.Objects;
using Penguin.Web.Dynamic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Penguin.Cms.Modules.Dynamic.Services
{
    /// <summary>
    /// A service used to find information relevant to rendering MetaObjects dynamically
    /// </summary>
    public class DynamicEditorService : ISelfRegistering
    {
        private static readonly ConcurrentDictionary<string, string?> Views = new ConcurrentDictionary<string, string?>();

        /// <summary>
        /// The provider used for finding files
        /// </summary>
        protected IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Constructs a new instance of the DynamicEditorService using the provided IFileProvider
        /// </summary>
        /// <param name="fileProvider">The provider used for finding files</param>
        public DynamicEditorService(IFileProvider fileProvider)
        {
            this.FileProvider = fileProvider;
        }

        /// <summary>
        /// Joins a string, object dictionary into a string representation (data-{name}="{value}") list for use in html elements}
        /// </summary>
        /// <param name="Attributes">The dictionary containing the attributes to generate a string for</param>
        /// <returns>The HtmlString representation of the provided attributes prefixed with data-</returns>
        public static HtmlString JoinAttributes(Dictionary<string, object> Attributes)
        {
            if (Attributes is null)
            {
                throw new ArgumentNullException(nameof(Attributes));
            }

            string attributeString = " ";

            foreach (KeyValuePair<string, object> attribute in Attributes)
            {
                if (attribute.Value is null)
                {
                    attributeString = $" data-{attribute.Key}{attributeString}";
                }
                else
                {
                    attributeString = $" data-{attribute.Key}=\"{attribute.Value}\"{attributeString}";
                }
            }

            return new HtmlString(attributeString);
        }

        /// <summary>
        /// Returns the handler that should be used to render a given MetaObject
        /// </summary>
        /// <param name="metaObject">The MetaObject to find a handler for</param>
        /// <param name="requestContext">The current request context</param>
        /// <param name="displayType">The type used when finding the handler, if not the MetaObject type</param>
        /// <returns>A result object that may point to either an action or a view to be used when rendering the object</returns>
        public EditorHandlerResult FindHandler(IMetaObject metaObject, DisplayContexts requestContext, IMetaType? displayType = null)
        {
            if (metaObject is null)
            {
                throw new ArgumentNullException(nameof(metaObject));
            }

            if (metaObject.Property?.HasAttribute<ForceDynamicAttribute>() ?? false)
            {
                return new DynamicEditorResult();
            }

            return GetAction(metaObject, requestContext, displayType) ??
                   this.GetView(metaObject, requestContext, displayType) ??
                   (metaObject.GetCoreType() == CoreType.Value ?
                                                new StaticValueResult() :
                                                new DynamicEditorResult() as EditorHandlerResult);
        }

        protected static DynamicActionResult? GetAction(IMetaObject metaObject, DisplayContexts requestContext, IMetaType? displayType = null)
        {
            if (metaObject is null)
            {
                throw new ArgumentNullException(nameof(metaObject));
            }

            displayType ??= metaObject.Type;

            foreach (MethodInfo methodInfo in TypeFactory.GetDerivedTypes(typeof(Controller)).SelectMany(m => m.GetMethods()))
            {
                if (!metaObject.IsRoot())
                {
                    if (methodInfo.GetCustomAttribute<DynamicPropertyHandlerAttribute>() is DynamicPropertyHandlerAttribute propertyAttribute && propertyAttribute.DisplayContexts.HasFlag(requestContext))
                    {
                        if (metaObject.Property.Name == propertyAttribute.PropertyName && metaObject.Parent.Type.Is(propertyAttribute.Type))
                        {
                            return GetActionResult(methodInfo);
                        }
                    }
                }

                if (methodInfo.GetCustomAttribute<DynamicHandlerAttribute>() is DynamicHandlerAttribute attribute && attribute.DisplayContexts.HasFlag(requestContext))
                {
                    if (attribute.ToHandle.FirstOrDefault(t => displayType.Is(t)) is Type handledType)
                    {
                        return GetActionResult(methodInfo);
                    }
                }
            }

            IMetaObject? customRouteAttribute = metaObject?.Property
                                           ?.Attributes
                                           ?.Where(a => a.Type.FullName == typeof(CustomRouteAttribute).FullName)
                                           ?.Where(a => a.Instance[nameof(CustomRouteAttribute.Context)].GetValue<DisplayContexts>().HasFlag(DisplayContexts.Edit))
                                           ?.Select(a => a.Instance)
                                           ?.SingleOrDefault();

            if (customRouteAttribute != null)
            {
                return new DynamicActionResult(customRouteAttribute[nameof(CustomRouteAttribute.RouteValues)].ToDictionary<string, object>());
            }

            return null;
        }

        /// <summary>
        /// Gets the view result containing editor view information for the current metaObject
        /// </summary>
        /// <param name="metaObject">The MetaObject to get view information for</param>
        /// <param name="requestContext">The current RequestContext</param>
        /// <param name="displayType">The type to be used when finding the view, if not the MetaObject type</param>
        /// <returns>A result containing editor view information</returns>
        protected DynamicViewResult? GetView(IMetaObject metaObject, DisplayContexts requestContext, IMetaType? displayType = null)
        {
            if (metaObject is null)
            {
                throw new ArgumentNullException(nameof(metaObject));
            }

            string BasePath = $"/Areas/Admin/Views/{requestContext}/";

            if (metaObject.IsRoot())
            {
                return null;
            }

            IMetaProperty property = metaObject.Property;
            displayType ??= GetDisplayType(metaObject);

            string Key = $"{displayType.AssemblyQualifiedName}+{property?.Name}+{requestContext}";

            if (!Views.TryGetValue(Key, out string? path))
            {
                DynamicRenderer renderer = new DynamicRenderer(new DynamicRendererSettings(displayType, property, this.FileProvider)
                {
                    BasePath = BasePath
                });

                if (renderer.IsDynamic)
                {
                    _ = Views.TryAdd(Key, null);
                }
                else
                {
                    path = renderer.MatchedPath;
                    _ = Views.TryAdd(Key, path);
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            else
            {
                return new DynamicViewResult(path);
            }
        }

        private static DynamicActionResult GetActionResult(MethodInfo methodInfo)
        {
            if (methodInfo is null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            Dictionary<string, object> routeData = new Dictionary<string, object>
                        {
                            { "controller", methodInfo.ReflectedType.Name.Replace("Controller", "") },
                            { "action", methodInfo.Name }
                        };

            string area = string.Empty;

            if (methodInfo.ReflectedType.GetCustomAttribute<AreaAttribute>() is AreaAttribute areaA)
            {
                area = areaA.RouteValue;
            }

            routeData.Add("area", area);

            return new DynamicActionResult(routeData);
        }

        private static IMetaType GetDisplayType(IMetaObject metaObject)
        {
            if (metaObject.Property.HasAttribute<DisplayTypeAttribute>())
            {
                string displayTypeName = metaObject.Property.Attribute<DisplayTypeAttribute>()[nameof(DisplayTypeAttribute.Name)].Value;

                return new MetaType()
                {
                    Namespace = displayTypeName.Contains('.') ? displayTypeName.ToLast(".") : "",
                    Name = displayTypeName.Contains('.') ? displayTypeName.FromLast(".") : displayTypeName,
                    AssemblyQualifiedName = displayTypeName,
                    FullName = displayTypeName
                };
            }
            else
            {
                return metaObject.Type ?? metaObject.Property.Type;
            }
        }
    }
}