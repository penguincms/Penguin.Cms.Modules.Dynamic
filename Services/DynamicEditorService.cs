using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Penguin.Cms.Abstractions.Attributes;
using Penguin.Cms.Modules.Dynamic.Attributes;
using Penguin.Cms.Modules.Dynamic.Rendering;
using Penguin.Cms.Web.Extensions;
using Penguin.DependencyInjection.Abstractions.Interfaces;
using Penguin.Extensions.Strings;
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
    public class DynamicEditorService : ISelfRegistering
    {
        protected IFileProvider FileProvider { get; set; }

        public static ConcurrentDictionary<string, string> Views = new ConcurrentDictionary<string, string>();

        public DynamicEditorService(IFileProvider fileProvider)
        {
            FileProvider = fileProvider;
        }

        public static HtmlString JoinAttributes(Dictionary<string, object> Attributes)
        {
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

        public EditorHandlerResult FindHandler(IMetaObject metaObject, DisplayContexts requestContext, IMetaType displayType = null)
        {
            if (metaObject.Property?.HasAttribute<ForceDynamicAttribute>() ?? false)
            {
                return new DynamicEditorResult();
            }

            return GetAction(metaObject, requestContext, displayType) as EditorHandlerResult ??
                   GetView(metaObject, requestContext, displayType) as EditorHandlerResult ??
                   (metaObject.GetCoreType() == CoreType.Value ?
                                                new StaticValueResult() as EditorHandlerResult :
                                                new DynamicEditorResult() as EditorHandlerResult);
        }

        protected DynamicActionResult GetAction(IMetaObject metaObject, DisplayContexts requestContext, IMetaType displayType = null)
        {
            displayType ??= metaObject.Type;

            foreach (MethodInfo methodInfo in TypeFactory.GetDerivedTypes(typeof(Controller)).SelectMany(m => m.GetMethods()))
            {
                if (!metaObject.IsRoot())
                {
                    if (methodInfo.GetCustomAttribute<DynamicPropertyHandlerAttribute>() is DynamicPropertyHandlerAttribute propertyAttribute && propertyAttribute.DisplayContexts.HasFlag(requestContext))
                    {
                        if (metaObject.Property.Name == propertyAttribute.PropertyName && metaObject.GetParent().Type.Is(propertyAttribute.Type))
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

        protected DynamicViewResult GetView(IMetaObject metaObject, DisplayContexts requestContext, IMetaType displayType = null)
        {
            string BasePath = $"/Areas/Admin/Views/{requestContext}/";

            if (metaObject.IsRoot())
            {
                return null;
            }

            IMetaProperty property = metaObject.Property;
            displayType ??= GetDisplayType(metaObject);

            string Key = $"{displayType.AssemblyQualifiedName}+{property?.Name}+{requestContext}";

            if (!Views.TryGetValue(Key, out string path))
            {
                DynamicRenderer renderer = new DynamicRenderer(new DynamicRendererSettings(displayType, property, FileProvider)
                {
                    BasePath = BasePath
                });

                if (renderer.IsDynamic)
                {
                    Views.TryAdd(Key, null);
                }
                else
                {
                    path = renderer.MatchedPath;
                    Views.TryAdd(Key, path);
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

        private DynamicActionResult GetActionResult(MethodInfo methodInfo)
        {
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

        private IMetaType GetDisplayType(IMetaObject metaObject)
        {
            if (metaObject.Property.HasAttribute<DisplayTypeAttribute>())
            {
                string displayTypeName = metaObject.Property.Attribute<DisplayTypeAttribute>()[nameof(DisplayTypeAttribute.Name)].Value;

                return new MetaType()
                {
                    Namespace = displayTypeName.Contains(".") ? displayTypeName.ToLast(".") : "",
                    Name = displayTypeName.Contains(".") ? displayTypeName.FromLast(".") : displayTypeName,
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