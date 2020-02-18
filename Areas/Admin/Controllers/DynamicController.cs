using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penguin.Cms.Core.Extensions;
using Penguin.Cms.Entities;
using Penguin.Cms.Errors;
using Penguin.Cms.Errors.Extensions;
using Penguin.Cms.Modules.Dynamic.Areas.Admin.Models;
using Penguin.Cms.Modules.Dynamic.Rendering;
using Penguin.Cms.Repositories.Interfaces;
using Penguin.Cms.Web.Extensions;
using Penguin.Extensions.Collections;
using Penguin.Extensions.Strings;
using Penguin.Messaging.Core;
using Penguin.Persistence.Abstractions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Persistence.Repositories.Interfaces;
using Penguin.Reflection;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using Penguin.Reflection.Serialization.Objects;
using Penguin.Web.Dynamic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Type = System.Type;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Controllers
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters")]
    public class DynamicController : ObjectManagementController<Entity>
    {
        private const string SUCCESSFUL_SAVE_MESSAGE = "The object was successfully saved";
        protected IRepository<AuditableError> ErrorRepository { get; set; }

        protected IFileProvider FileProvider { get; set; }
        protected MessageBus? MessageBus { get; set; }

        public DynamicController(IServiceProvider serviceProvider, IFileProvider fileProvider, IRepository<AuditableError> errorRepository, MessageBus? messageBus = null) : base(serviceProvider)
        {
            FileProvider = fileProvider;
            MessageBus = messageBus;
            ErrorRepository = errorRepository;
        }

        [HttpPost]
        public virtual ActionResult BatchCreate(BatchCreatePageModel model)
        {
            if (model is null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Type type = TypeFactory.GetTypeByFullName(model.Type, typeof(Entity), false);

            string Message = string.Empty;
            List<string> Existing = new List<string>();

            try
            {
                IEntityRepository thisRepo = (IEntityRepository)ServiceProvider.GetService(typeof(IRepository<>).MakeGenericType(type));

                using IWriteContext context = thisRepo.WriteContext();
                foreach (string thisExternalId in model.ExternalIds.TrimLines())
                {
                    if (thisRepo.Find(thisExternalId) is null)
                    {
                        if (Activator.CreateInstance(type) is Entity entity)
                        {
                            entity.ExternalId = thisExternalId;

                            thisRepo.Add(entity);
                        }
                    }
                    else
                    {
                        Existing.Add(thisExternalId);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorRepository.TryAdd(ex);

                this.AddMessage(ex.Message);
                return this.View(model);
            }

            if (Existing.Any())
            {
                this.AddMessage("Existing Items: " + string.Join(", ", Existing));
            }
            return this.RedirectToAction(nameof(List), new { type = model.Type });
        }

        [HttpGet]
        public virtual ActionResult BatchCreate(string Type) => this.View(new BatchCreatePageModel() { Type = Type });

        [HttpPost]
        public virtual ActionResult BatchEdit(UpdateListPageModel items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            //This will be used to hold the returned entites while we build our return object
            List<object> Entities = this.GetEntitiesByGuids(items.Guids.Select(s => Guid.Parse(s)).ToList());

            //We're gonna use this to determine the eventual return type of the list
            Type commonType = Entities.GetCommonType();

            DynamicRenderer renderer = new DynamicRenderer(new DynamicRendererSettings(commonType, FileProvider) { ExactOnly = true });

            BatchEditModelPageModel BEmodel = new BatchEditModelPageModel
            {
                Guids = items.Guids,
                Template = Activator.CreateInstance(commonType)
            };

            IMetaObject model = new MetaObject(BEmodel, Constructor);

            model.Hydrate();

            return this.View(model);
        }

        public virtual ActionResult BatchSave(string json)
        {
            Type? commonType = null;

            try
            {
                using IWriteContext context = ServiceProvider.GetService<IPersistenceContext>().WriteContext();
                List<object> Entities = this.GetEntitiesByGuids(JsonConvert.DeserializeObject<BatchEditModelPageModel>(json).Guids.Select(g => Guid.Parse(g)).ToList());

                commonType = Entities.GetCommonType();

                JObject obj = JObject.Parse(json);
                JToken jtok = obj[nameof(BatchEditModelPageModel.Template)];

                foreach (Entity entity in Entities)
                {
                    this.SaveJsonObject(jtok.ToString(), entity);
                }
            }
            catch (Exception ex)
            {
                ErrorRepository.TryAdd(ex);
                return this.Json(new { Response = new { Error = ex.Message } });
            }

            return this.Json(new { Response = new { Redirect = $"/Admin/List/{commonType?.FullName}" } });
        }

        public List<object> GetEntitiesByGuids(List<Guid> ToFind)
        {
            if (ToFind is null)
            {
                throw new ArgumentNullException(nameof(ToFind));
            }

            //This will be used to hold the returned entites while we build our return object
            List<object> Entities = new List<object>();

            //Start with the framework context
            //DynamicContext thisContext = ServiceProvider.GetService<DynamicContext>();

            //Grabbing all the possible DBSetTypes. This should be an accurate list of all repository types
            List<Type> EntityTypes = TypeFactory.GetDerivedTypes(typeof(Entity)).ToList();

            int LastFindCount = ToFind.Count;

            do
            {
                //For each Type
                foreach (Type type in EntityTypes.ToList())
                {
                    //If its not an auditableEntity, keep moving.
                    //This check should be made dynamic to the parameter itself instead of being hardcoded
                    if (!typeof(AuditableEntity).IsAssignableFrom(type))
                    {
                        EntityTypes.Remove(type);
                        continue;
                    }

                    //Grab a repository that matches
                    IEntityRepository thisRepo = (IEntityRepository)ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(type));

                    //If theres no repo for it, dont check again
                    if (thisRepo is null)
                    {
                        EntityTypes.Remove(type);
                    }
                    else
                    {
                        //With this repo, check for every guid
                        foreach (Guid thisGuid in ToFind.ToList())
                        {
                            object ThisEntity = thisRepo.Find(thisGuid);

                            //if no matching entity was found, move on to the next repo.
                            //This should speed it up since MOST LIKELY all entities will be the same type
                            if (ThisEntity is null)
                            {
                                continue;
                            }
                            else
                            {
                                //If we have a matching entity, remove the guid from our find list
                                ToFind.Remove(thisGuid);
                                //And then add it to the output list
                                Entities.Add(ThisEntity);
                            }
                        }
                    }
                }
                //If we havent removed at least one entity from the list this iteration, blow up so we arent spinning our wheels forever
                if (LastFindCount == ToFind.Count)
                {
                    throw new Exception("Object to find count hasnt changed since last iteration");
                }
                else
                {
                    //If the list size has changed, make sure to update it;
                    LastFindCount = ToFind.Count;
                }

                //We have to loop because of the speed increase caused by the continue. Since we skip repos that dont
                //return a the first result, we want to make sure we come back to them later just in case
            } while (ToFind.Any());

            return Entities;
        }

        public ActionResult Save(string json)
        {
            string Referrer = this.Request.Headers["Referer"];

            Entity toSave;
            IKeyedObjectRepository typeRepository;
            Type t;

            JObject tempEntity = JObject.Parse(json);
            string TypeString = (string)tempEntity["TypeName"];
            //Cheap hack for redirect. Code proper redirect for new entity
            bool ExistingEntity = false;

            int Id = (int)tempEntity["_Id"];

            try
            {
                (t, typeRepository, toSave) = FindOrCreateEntity(TypeString, Id);

                using IWriteContext context = typeRepository.WriteContext();

                this.SaveJsonObject(json, toSave, t);
            }
            catch (Exception ex)
            {
                ErrorRepository.TryAdd(ex);
                return this.Json(new { Response = new { Error = ex.Message } });
            }

            if (ExistingEntity)
            {
                this.AddMessage(SUCCESSFUL_SAVE_MESSAGE);
                return this.Json(new { Response = new { Redirect = Referrer } });
            }
            else
            {
                //ToDo: Move this HTML into a view
                return this.Json(new { Response = new { Body = $"The object was successfully saved <br /> <a href=\"{Referrer}\">Create Another</a> <br /> <a href=\"/Admin/Edit/{TypeString}/{toSave._Id}\">Make changes to {toSave?.ExternalId ?? "This Entity"}</a> <br />" } });
            }
        }

        public void SaveJsonObject(string json, Entity toSave, Type? t = null)
        {
            if (t is null)
            {
                t = TypeFactory.GetType(toSave);
            };

            if (ServiceProvider.GetRepositoryForType(t) is IRepository typeRepository)
            {
                toSave = this.UpdateJsonObject(json, toSave, t);
                typeRepository.AddOrUpdate(toSave);
            }
            else
            {
                throw new Exception($"Typed repository not found for type {t}");
            }
        }

        public ActionResult Submit(string json, string type)
        {
            string Referrer = this.Request.Headers["Referer"];
            Entity toSave; ;
            //Cheap hack for redirect. Code proper redirect for new entity
            bool ExistingEntity = false;

            JObject tempEntity = JObject.Parse(json);

            int Id = 0;

            if (tempEntity.Properties().Any(p => p.Name == nameof(KeyedObject._Id)))
            {
                Id = (int)tempEntity[nameof(KeyedObject._Id)];
            }

            Type t;

            IKeyedObjectRepository typeRepository;

            try
            {
                (t, typeRepository, toSave) = FindOrCreateEntity(type, Id);

                using IWriteContext context = typeRepository.WriteContext();

                this.SaveJsonObject(json, toSave, t);
            }
            catch (Exception ex)
            {
                ErrorRepository.TryAdd(ex);
                return this.Json(new { Response = new { Error = ex.Message } });
            }

            if (ExistingEntity)
            {
                this.AddMessage(SUCCESSFUL_SAVE_MESSAGE);
                return this.Json(new { Response = new { Redirect = Referrer } });
            }
            else
            {
                //ToDo: Move this HTML into a view
                return this.Json(new { Id = toSave._Id, Response = new { Body = $"The object was successfully saved <br /> <a href=\"{Referrer}\">Create Another</a> <br /> <a href=\"/Admin/Edit/{type}/{toSave._Id}\">Make changes to {toSave?.ExternalId ?? "This Entity"}</a> <br />" } });
            }
        }

        public void UpdateEntityList(IList list)
        {
            if (list != null)
            {
                foreach (Entity thisEntity in list.Cast<Entity>().ToList())
                {
                    if (thisEntity != null && this.RetrieveSavedEntity(thisEntity) is Entity refreshed)
                    {
                        list.Add(refreshed);
                    }

                    list.Remove(thisEntity);
                }
            }
        }

        public KeyedObject UpdateJsonObject(string json, KeyedObject toSave, Type? t = null)
        {
            if (toSave is null)
            {
                throw new ArgumentNullException(nameof(toSave));
            }

            if (ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(t ?? toSave.GetType()) is IKeyedObjectRepository repository)
            {
                if (toSave._Id != 0)
                {
                    toSave = repository.Find(toSave._Id);
                }

                JsonSerializerSettings serializerSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

                serializerSettings.Converters.Add(new LazyLoadForce());

                JsonConvert.PopulateObject(json, toSave, serializerSettings);

                this.UpdateProperties(json, toSave, t);

                repository.Update(toSave);

                return toSave;
            }
            else
            {
                throw new Exception($"Repository not found for type {t}");
            }
        }

        public Entity UpdateJsonObject(string json, Entity toSave, Type? t = null)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

            serializerSettings.Converters.Add(new LazyLoadForce());

            JsonConvert.PopulateObject(json, toSave, serializerSettings);

            this.UpdateProperties(json, toSave, t);

            return toSave;
        }

        public void UpdateKeyedObjectList(IList list, JToken listJson)
        {
            if (listJson is null)
            {
                throw new ArgumentNullException(nameof(listJson));
            }

            list ??= new List<object>();

            if (list is null)
            {
                throw new Exception("What the fuck?");
            }

            List<KeyedObject> oldObjects = list.Cast<KeyedObject>().ToList();

            list.Clear();

            if (list != null)
            {
                int i = 0;
                foreach (KeyedObject thisObject in oldObjects)
                {
                    if (thisObject != null)
                    {
                        KeyedObject newObject = this.UpdateJsonObject(listJson[i].ToString(), thisObject);
                        list.Add(newObject);
                    }
                    i++;
                }
            }
        }

        public virtual ActionResult UpdateList()
        {
            UpdateListPageModel model = new UpdateListPageModel();

            foreach (string key in this.Request.Form.Keys.Where(s => Guid.TryParse(s, out Guid _)))
            {
                model.Guids.Add(key);
            }

            return this.View(model);
        }

        public void UpdateProperties(string json, KeyedObject toSave, Type? t = null)
        {
            if (toSave is null)
            {
                throw new ArgumentNullException(nameof(toSave));
            }

            t ??= toSave.GetType();

            if (t is null)
            {
                throw new Exception("What the fuck?");
            }

            foreach (PropertyInfo thisProperty in t.GetProperties())
            {
                if (thisProperty.GetGetMethod() == null)
                {
                    continue;
                }

                if (thisProperty.PropertyType.GetInterface("IEnumerable") != null)
                {
                    Type[] GenericArguments = thisProperty.PropertyType.GetGenericArguments();

                    if (!GenericArguments.Any())
                    {
                        continue;
                    }

                    Type listType = GenericArguments[0];

                    if (thisProperty.GetValue(toSave) is IList toUpdate)
                    {
                        if (listType.IsSubclassOf(typeof(Entity)))
                        {
                            this.UpdateEntityList(toUpdate!);
                        }
                        else if (listType.IsSubclassOf(typeof(KeyedObject)))
                        {
                            JToken newValue = JObject.Parse(json)[thisProperty.Name];
                            if (toUpdate.Count > 0 && newValue != null)
                            {
                                this.UpdateKeyedObjectList(toUpdate, newValue);
                            }
                        }
                    }
                }
                else if (thisProperty.PropertyType.IsSubclassOf(typeof(AuditableEntity)))
                {
                    if (thisProperty.GetValue(toSave) is AuditableEntity thisEntity && this.RetrieveSavedEntity(thisEntity) is AuditableEntity existingValue)
                    {
                        thisProperty.SetValue(toSave, existingValue);
                    }
                }
                else if (thisProperty.PropertyType.IsSubclassOf(typeof(KeyedObject)))
                {
                    //Dont fuck with this property if it wasn't sent over by the client!
                    if (JObject.Parse(json)[thisProperty.Name]?.ToString() is string sentProp && thisProperty.GetValue(toSave) is KeyedObject val)
                    {
                        KeyedObject thisObject = this.UpdateJsonObject(sentProp, val, thisProperty.PropertyType);

                        thisProperty.SetValue(toSave, thisObject);
                    }
                }
            }
        }

        private (Type, IKeyedObjectRepository, Entity) FindOrCreateEntity(string TypeString, int Id)
        {
            Type t = TypeFactory.GetTypeByFullName(TypeString, typeof(Entity));

            IKeyedObjectRepository? typeRepository = ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(t);

            if (typeRepository is null)
            {
                throw new Exception($"Typed repository not found for type {t}");
            }

            if ((Id == 0 ? Activator.CreateInstance(t) : typeRepository.Find(Id)) is Entity entity)
            {
                return (t, typeRepository, entity);
            }
            else
            {
                throw new Exception($"Unable to find or create entity instance of type {t}");
            }
        }
    }
}