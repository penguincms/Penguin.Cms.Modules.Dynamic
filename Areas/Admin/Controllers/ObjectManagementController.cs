using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Penguin.Cms.Core.Extensions;
using Loxifi;
using Penguin.Cms.Core.Services;
using Penguin.Cms.Entities;
using Penguin.Cms.Modules.Admin.Areas.Admin.Controllers;
using Penguin.Cms.Modules.Dynamic.Areas.Admin.Models;
using Penguin.Cms.Repositories.Interfaces;
using Penguin.Cms.Web.Modules;
using Penguin.Persistence.Abstractions;
using Penguin.Persistence.Repositories.Interfaces;
using Penguin.Reflection;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using Penguin.Reflection.Serialization.Abstractions.Wrappers;
using Penguin.Reflection.Serialization.Constructors;
using Penguin.Security.Abstractions.Constants;
using Penguin.Security.Abstractions.Interfaces;
using Penguin.Web.Security.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Controllers
{
    [RequiresRole(RoleNames.LOGGED_IN)]
    public class ObjectManagementController<T> : AdminController where T : Entity
    {
        //protected AuditEntryRepository AuditEntryRepository { get; set; }
        //protected UserSession UserSession { get; set; }

        protected ComponentService ComponentService { get; set; }

        public ObjectManagementController(IServiceProvider serviceProvider, IUserSession userSession) : base(serviceProvider, userSession)
        {
            ComponentService = new ComponentService(serviceProvider);
            //UserSession = userSession;
            //AuditEntryRepository = auditEntryRepository;
        }
        public virtual ActionResult Edit(int? id, string? type = null)
        {
            System.Type t = type is null ? typeof(T) : TypeFactory.Default.GetTypeByFullName(type);

            object? toEdit = id.HasValue
                ? t.IsSubclassOf(typeof(KeyedObject))
                    ? (object?)(ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(t)?.Find(id.Value))
                    : throw new Exception("Support for editing objects that are not derived from KeyedObject is not available in this version")
                : Activator.CreateInstance(t);
            return toEdit is null
                ? throw new NullReferenceException($"Unable to find or instantiate object of type {t} with key {id}")
                : Edit(toEdit);
        }

        public virtual ActionResult List(string type, int count = 20, int page = 0, string text = "")
        {
            System.Type listType = type is null ? typeof(T) : TypeFactory.Default.GetTypeByFullName(type, typeof(object), false);

            MetaConstructor c = Constructor;

            DynamicListRenderPageModel model = new()
            {
                PagedList = GenerateList<IMetaObject>(listType, count, page, text, (o) => new MetaObjectHolder(o)),
                Type = type ?? string.Empty
            };

            return View(model);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.RouteData.Values.ContainsKey("KnownController"))
            {
                context.RouteData.Values.Add("KnownController", "True");
            }

            base.OnActionExecuting(context);
        }

        public Entity? RetrieveSavedEntity(Entity temporaryEntity)
        {
            if (temporaryEntity is null)
            {
                return null;
            }

            Type lt = TypeFactory.Default.GetTypeByFullName(temporaryEntity.TypeName, typeof(Entity));

            IEntityRepository ltTypeRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(lt));

            Entity? existingValue;
            //Maybe we have the ID
            if (temporaryEntity._Id != 0)
            {
                existingValue = ltTypeRepository.Find(temporaryEntity._Id) as Entity;
            }
            else // At least we have the guid
            {
                existingValue = ltTypeRepository.Find(temporaryEntity.Guid);

                if (existingValue == null) //No guid hit? Maybe its a child type?
                {
                    IEnumerable<Type> toSearch = TypeFactory.Default.GetDerivedTypes(lt);

                    foreach (Type t in toSearch)
                    {
                        ltTypeRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(t));

                        if (ltTypeRepository.IsValid)
                        {
                            Entity guidMatch = ltTypeRepository.Find(temporaryEntity.Guid);

                            if (guidMatch != null)
                            {
                                return guidMatch;
                            }
                        }
                    }
                }
            }

            return existingValue ?? temporaryEntity;
        }

        public ActionResult Search(string type, string term, int limit = 10)
        {
            List<Type> typesToSearch = TypeFactory.Default.GetDerivedTypes(TypeFactory.Default.GetTypeByFullName(type)).ToList();

            typesToSearch.Add(TypeFactory.Default.GetTypeByFullName(type));

            typesToSearch = typesToSearch.Distinct().ToList();

            List<Entity> results = new();

            foreach (Type t in typesToSearch)
            {
                if (results.Count < limit)
                {
                    IEntityRepository? TypedRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(t));

                    if (TypedRepository != null && TypedRepository.IsValid)
                    {
                        IEnumerable<Entity> found = TypedRepository.Where<Entity>(e => e.ExternalId.Contains(term)).Where(e => !results.Any(r => r.Guid == e.Guid));

                        results.AddRange(found);
                    }
                }
            }

            results = results.Count > limit ? results.Take(limit).ToList() : results.ToList();

            string s = JsonConvert.SerializeObject(results, Formatting.None, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            return Content(s);
        }

        protected virtual ActionResult Edit(object o)
        {
            if (o is Entity e)
            {
                IMetaObject i = new MetaObjectHolder(o);

                EntityViewModel<IMetaObject> model = new(i, ComponentService.GetComponents<ViewModule, Entity>(e).ToList());

                return View("DynamicEditor", model);
            }
            else
            {
                throw new ArgumentNullException(nameof(o));
            }
        }
    }
}