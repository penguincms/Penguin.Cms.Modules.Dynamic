﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Penguin.Cms.Core.Extensions;
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
using Penguin.Reflection.Serialization.Constructors;
using Penguin.Reflection.Serialization.Objects;
using Penguin.Security.Abstractions.Attributes;
using Penguin.Security.Abstractions.Constants;
using Penguin.Web.Security.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Controllers
{
    [RequiresRole(RoleNames.LoggedIn)]
    public class ObjectManagementController<T> : AdminController where T : Entity
    {
        //protected AuditEntryRepository AuditEntryRepository { get; set; }
        //protected UserSession UserSession { get; set; }

        protected ComponentService ComponentService { get; set; }

        public ObjectManagementController(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            ComponentService = new ComponentService(serviceProvider);
            //UserSession = userSession;
            //AuditEntryRepository = auditEntryRepository;
        }

        [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
        public virtual ActionResult Edit(int? id, string? type = null)
        {
            System.Type t = type is null ? typeof(T) : TypeFactory.GetTypeByFullName(type);

            object? toEdit;

            if (id.HasValue)
            {
                if (t.IsSubclassOf(typeof(KeyedObject)))
                {
                    toEdit = ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(t)?.Find(id.Value);
                }
                else
                {
                    throw new Exception("Support for editing objects that are not derived from KeyedObject is not available in this version");
                    //DbContext targetContext = ContextHelper.GetContextForType(t);

                    //System.Reflection.PropertyInfo Key = ContextHelper.GetKeyForType(t);

                    //DbSet targetSet = targetContext.Set(t);

                    //Task<List<object>> results = targetSet.SqlQuery($"select * from [{targetContext.GetTableName(t)}] where {Key.Name} = {_id.Value}").ToListAsync();

                    //results.Wait();

                    //toEdit = results.Result.Single();
                }
            }
            else
            {
                toEdit = Activator.CreateInstance(t);
            }

            if (toEdit is null)
            {
                throw new NullReferenceException($"Unable to find or instantiate object of type {t} with key {id}");
            }

            return this.Edit(toEdit);
        }

        public virtual ActionResult List(string type, int count = 20, int page = 0, string text = "")
        {
            System.Type listType = type is null ? typeof(T) : TypeFactory.GetTypeByFullName(type, typeof(object), false);

            MetaConstructor c = Constructor;

            DynamicListRenderPageModel model = new DynamicListRenderPageModel()
            {
                PagedList = this.GenerateList<IMetaObject>(listType, count, page, text, (o) =>
                {
                    MetaObject x = new MetaObject(o, c);

                    x.Hydrate();
                    return x;
                }),
                Type = type ?? string.Empty
            };

            return this.View(model);
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext is null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            if (!filterContext.RouteData.Values.ContainsKey("KnownController"))
            {
                filterContext.RouteData.Values.Add("KnownController", "True");
            }

            base.OnActionExecuting(filterContext);
        }

        public Entity? RetrieveSavedEntity(Entity temporaryEntity)
        {
            if (temporaryEntity is null)
            {
                return null;
            }

            Type lt = TypeFactory.GetTypeByFullName(temporaryEntity.TypeName, typeof(Entity));

            IEntityRepository ltTypeRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(lt));

            Entity? existingValue;
            //Maybe we have the ID
            if (temporaryEntity._Id != 0)
            {
                existingValue = (ltTypeRepository.Find(temporaryEntity._Id) as Entity);
            }
            else // At least we have the guid
            {
                existingValue = (ltTypeRepository.Find(temporaryEntity.Guid) as Entity);

                if (existingValue == null) //No guid hit? Maybe its a child type?
                {
                    IEnumerable<Type> toSearch = TypeFactory.GetDerivedTypes(lt);

                    foreach (Type t in toSearch)
                    {
                        ltTypeRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(t));

                        if (ltTypeRepository.IsValid)
                        {
                            Entity guidMatch = (Entity)ltTypeRepository.Find(temporaryEntity.Guid);

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
            List<Type> typesToSearch = TypeFactory.GetDerivedTypes(TypeFactory.GetTypeByFullName(type)).ToList();

            typesToSearch.Add(TypeFactory.GetTypeByFullName(type));

            typesToSearch = typesToSearch.Distinct().ToList();

            List<Entity> results = new List<Entity>();

            foreach (Type t in typesToSearch)
            {
                if (results.Count() < limit)
                {
                    IEntityRepository? TypedRepository = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(t));

                    if (TypedRepository != null && TypedRepository.IsValid)
                    {
                        IEnumerable<Entity> found = TypedRepository.Where<Entity>(e => e.ExternalId.Contains(term)).Where(e => !results.Any(r => r.Guid == e.Guid));

                        results.AddRange(found);
                    }
                }
            }

            if (results.Count() > limit)
            {
                results = results.Take(limit).ToList();
            }
            else
            {
                results = results.ToList();
            }

            string s = JsonConvert.SerializeObject(results, Formatting.None, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            return this.Content(s);
        }

        protected virtual ActionResult Edit(object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            MetaObject i = MetaObject.FromConstructor(Constructor.Clone(o));

            i.Hydrate();

            EntityViewModel<IMetaObject> model = new EntityViewModel<IMetaObject>(i, ComponentService.GetComponents<ViewModule, Entity>(o as Entity).ToList());

            return this.View("DynamicEditor", model);
        }
    }
}