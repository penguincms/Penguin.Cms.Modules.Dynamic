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
using Penguin.Cms.Repositories.Interfaces;
using Penguin.Cms.Web.Extensions;
using Penguin.Extensions.Collections;
using Penguin.Extensions.Strings;
using Penguin.Json.Extensions;
using Penguin.Messaging.Core;
using Penguin.Persistence.Abstractions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Persistence.Repositories.Interfaces;
using Penguin.Reflection;
using Penguin.Reflection.Serialization.Objects;
using Penguin.Security.Abstractions.Interfaces;
using Penguin.Web.Dynamic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Type = System.Type;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Controllers
{

    public class DynamicController : ObjectManagementController<Entity>
    {
        private const string SUCCESSFUL_SAVE_MESSAGE = "The object was successfully saved";

        /// <summary>
        /// A persistence repository used to log errors
        /// </summary>
        protected IRepository<AuditableError> ErrorRepository { get; set; }

        /// <summary>
        /// An IFileProvider implementation
        /// </summary>
        protected IFileProvider FileProvider { get; set; }

        /// <summary>
        /// A message bus instance used to send system messages
        /// </summary>
        protected MessageBus? MessageBus { get; set; }

        /// <summary>
        /// A controller used to dynamically edit objects used by the CMS
        /// </summary>
        /// <param name="serviceProvider">An IServiceProvider implementation</param>
        /// <param name="fileProvider">An IFileProvider implementation</param>
        /// <param name="errorRepository">A persistence repository used to log errors</param>
        /// <param name="messageBus">A message bus instance used to send system messages</param>
        public DynamicController(IServiceProvider serviceProvider, IFileProvider fileProvider, IRepository<AuditableError> errorRepository, IUserSession userSession, MessageBus? messageBus = null) : base(serviceProvider, userSession)
        {
            this.FileProvider = fileProvider;
            this.MessageBus = messageBus;
            this.ErrorRepository = errorRepository;
        }

        /// <summary>
        /// Accepts a batch create model and uses the provided information to create new entities with external ids matching those provided
        /// </summary>
        /// <param name="model">The batch create model containing the Ids</param>
        /// <returns>A view containing the result status of the operation</returns>
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
                IEntityRepository thisRepo = (IEntityRepository)this.ServiceProvider.GetService(typeof(IRepository<>).MakeGenericType(type));

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
                this.ErrorRepository.TryAdd(ex);

                this.AddMessage(ex.Message);
                return this.View(model);
            }

            if (Existing.Any())
            {
                this.AddMessage("Existing Items: " + string.Join(", ", Existing));
            }
            return this.RedirectToAction(nameof(List), new { type = model.Type });
        }

        /// <summary>
        /// Returns an action result used to enter external ids of new objects to create, of the provided type
        /// </summary>
        /// <param name="Type">The type of the objects to create using the editor</param>
        /// <returns>An action result used to enter external ids of new objects to create, of the provided type</returns>
        [HttpGet]
        public virtual ActionResult BatchCreate(string Type) => this.View(new BatchCreatePageModel() { Type = Type });

        /// <summary>
        /// Returns an editor view used to edit multiple objects at once
        /// </summary>
        /// <param name="items">A page model containing the guids of the items to edit</param>
        /// <returns>An editor view used to edit multiple objects at once</returns>
        [HttpPost]
        public virtual ActionResult BatchEdit(UpdateListPageModel items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            //This will be used to hold the returned entities while we build our return object
            List<object> Entities = this.GetEntitiesByGuids(items.Guids.Select(s => Guid.Parse(s)).ToList());

            //We're gonna use this to determine the eventual return type of the list
            Type commonType = Entities.GetCommonType();

            DynamicRenderer renderer = new DynamicRenderer(new DynamicRendererSettings(commonType, this.FileProvider) { ExactOnly = true });

            BatchEditModelPageModel BEmodel = new BatchEditModelPageModel
            {
                Guids = items.Guids,
                Template = Activator.CreateInstance(commonType)
            };

            MetaObject model = new MetaObject(BEmodel, Constructor);

            model.Hydrate();

            return this.View(model);
        }

        /// <summary>
        /// Updates all entities with guids in the JObject.Guids property using the provided Json
        /// </summary>
        /// <param name="json">Json string containing the new property values to use when updating the objects</param>
        /// <returns>A success state representation, or a redirect</returns>
        public virtual ActionResult BatchSave(string json) //Smashing the guids into the object is bad. Fix this
        {
            Type? commonType = null;

            try
            {
                using IWriteContext context = this.ServiceProvider.GetService<IPersistenceContext>().WriteContext();
                List<object> Entities = this.GetEntitiesByGuids(JsonConvert.DeserializeObject<BatchEditModelPageModel>(json).Guids.Select(g => Guid.Parse(g)).ToList());

                commonType = Entities.GetCommonType();

                JObject obj = JObject.Parse(json);
                JToken? jtok = obj[nameof(BatchEditModelPageModel.Template)];

                if (jtok is null)
                {
                    throw new Exception("No template object found on posted json");
                }

                foreach (Entity entity in Entities)
                {
                    this.SaveJsonObject(jtok.ToString(), entity, new DynamicSaveCache());
                }
            }
            catch (Exception ex)
            {
                this.ErrorRepository.TryAdd(ex);
                return this.Json(new { Response = new { Error = ex.Message } });
            }

            return this.Json(new { Response = new { Redirect = $"/Admin/List/{commonType?.FullName}" } });
        }

        /// <summary>
        /// Returns a list of entities with Guids matching the provided list
        /// </summary>
        /// <param name="ToFind">The list of Guids of the entities to find</param>
        /// <returns>A list of objects containing the entities</returns>
        public List<object> GetEntitiesByGuids(List<Guid> ToFind)
        {
            if (ToFind is null)
            {
                throw new ArgumentNullException(nameof(ToFind));
            }

            //This will be used to hold the returned entities while we build our return object
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
                    //If its not an Entity, keep moving.
                    //This check should be made dynamic to the parameter itself instead of being hardcoded
                    if (!typeof(Entity).IsAssignableFrom(type))
                    {
                        EntityTypes.Remove(type);
                        continue;
                    }

                    //Grab a repository that matches
                    IEntityRepository thisRepo = (IEntityRepository)this.ServiceProvider.GetService(typeof(IEntityRepository<>).MakeGenericType(type));

                    //If there's no repository for it, dont check again
                    if (thisRepo is null)
                    {
                        EntityTypes.Remove(type);
                    }
                    else
                    {
                        //With this repository, check for every guid
                        foreach (Guid thisGuid in ToFind.ToList())
                        {
                            object ThisEntity = thisRepo.Find(thisGuid);

                            //if no matching entity was found, move on to the next repository.
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

                //We have to loop because of the speed increase caused by the continue. Since we skip repositories that dont
                //return a the first result, we want to make sure we come back to them later just in case
            } while (ToFind.Any());

            return Entities;
        }

        /// <summary>
        /// Action accepting submission of json object along with internal type, used to add or update keyed object or entity
        /// </summary>
        /// <param name="json">The json representation of the object to save</param>
        /// <returns>An action result containing the success state, or a redirect to the posting url</returns>
        [DisableRequestSizeLimit]
        public ActionResult Save(string json)
        {
            string Referrer = this.Request.Headers["Referer"];

            Entity toSave = null;

            JObject tempEntity = JObject.Parse(json);

            string? TypeString = (string?)tempEntity["TypeName"];

            if (TypeString is null)
            {
                throw new Exception("Unable to find type string on json object");
            }

            //Cheap hack for redirect. Code proper redirect for new entity
            bool ExistingEntity = false;

            TryGetId(tempEntity, out int Id);

            try
            {
                using DynamicWriteContext context = new DynamicWriteContext(TypeString, Id, this.ServiceProvider);

                this.SaveJsonObject(json, context.Entity, new DynamicSaveCache(), context.EntityType);

                toSave = context.Entity;
            }
            catch (Exception ex)
            {
                this.ErrorRepository.TryAdd(ex);
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
                return this.Json(new { Response = new { Body = $"The object was successfully saved <br /> <a href=\"{Referrer}\">Create Another</a> <br /> <a href=\"/Admin/Edit/{TypeString}/{toSave?._Id}\">Make changes to {toSave?.ExternalId ?? "This Entity"}</a> <br />" } });
            }
        }

        /// <summary>
        /// Updates the provided entity using a json string, and saves it to the registered persistence context
        /// </summary>
        /// <param name="json">The json string containing the new property values</param>
        /// <param name="toSave">The target entity</param>
        /// <param name="t">An optional type override to use when finding the correct context</param>
        public void SaveJsonObject(string json, Entity toSave, DynamicSaveCache cache, Type? t = null)
        {
            if (t is null)
            {
                t = TypeFactory.GetType(toSave);
            };

            if (this.ServiceProvider.GetRepositoryForType(t) is IRepository typeRepository)
            {
                toSave = this.UpdateJsonObject(json, toSave, cache, t);
                typeRepository.AddOrUpdate(toSave);
            }
            else
            {
                throw new Exception($"Typed repository not found for type {t}");
            }
        }

        /// <summary>
        /// Action accepting submission of json object along with internal type, used to add or update keyed object or entity
        /// </summary>
        /// <param name="json">The json representation of the object to save</param>
        /// <param name="type">The object type being saved</param>
        /// <returns>An action result containing the success state, or a redirect to the posting url</returns>
        [DisableRequestSizeLimit]
        public ActionResult Submit(string json, string type)
        {
            string Referrer = this.Request.Headers["Referer"];
            //Cheap hack for redirect. Code proper redirect for new entity
            bool ExistingEntity = false;

            JObject tempEntity = JObject.Parse(json);

            if (tempEntity is null)
            {
                throw new NullReferenceException("Provided json returned null when parsed as object");
            }

            TryGetId(tempEntity, out int Id);

            Entity? toSave = null;

            try
            {
                using DynamicWriteContext context = new DynamicWriteContext(type, Id, this.ServiceProvider);

                this.SaveJsonObject(json, context.Entity, new DynamicSaveCache(), context.EntityType);

                toSave = context.Entity;
            }
            catch (Exception ex)
            {
                this.ErrorRepository.TryAdd(ex);
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
                return this.Json(new { Id = toSave?._Id, Response = new { Body = $"The object was successfully saved <br /> <a href=\"{Referrer}\">Create Another</a> <br /> <a href=\"/Admin/Edit/{type}/{toSave?._Id}\">Make changes to {toSave?.ExternalId ?? "This Entity"}</a> <br />" } });
            }
        }

        /// <summary>
        /// Refreshes the provided list of entities from the data source, so that they're attached to any persistence context
        /// </summary>
        /// <param name="list">An IList of entities to refresh</param>
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

        /// <summary>
        /// Uses the provided Json to update the given KeyedObject, and saves it in the repository for the type
        /// </summary>
        /// <param name="json">The json containing the new properties</param>
        /// <param name="toSave">The KeyedObject used as a target for the Json Update</param>
        /// <param name="t">An optional type used as an override for the requested object type when requesting the repository</param>
        /// <returns>An updated version of the object being saved</returns>
        public KeyedObject UpdateJsonObject(string json, KeyedObject toSave, DynamicSaveCache cache, Type? t = null, IKeyedObjectRepository repository = null, KeyedObject repositoryObject = null)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (toSave is null)
            {
                throw new ArgumentNullException(nameof(toSave));
            }

            if (cache is null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            repository ??= this.ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(t ?? toSave.GetType());

            if (repository != null)
            {
                if (toSave._Id != 0)
                {
                    toSave = repositoryObject ?? repository.Find(toSave._Id) ?? toSave;
                }

                this.UpdateProperties(json, toSave, cache, t);

                if (toSave._Id == 0)
                {
                    repository.Add(toSave);
                }

                return toSave;
            }
            else
            {
                throw new Exception($"Repository not found for type {t}");
            }
        }

        public Entity UpdateJsonObject(string json, Entity toSave, DynamicSaveCache cache, Type? t = null)
        {
            this.UpdateProperties(json, toSave, cache, t);

            return toSave;
        }

        /// <summary>
        /// Accepts a form post of Guids and uses that post to generate an intermediate page for updating a collection of objects
        /// </summary>
        /// <returns>The view for the intermediate page containing the update types allowed for object collections</returns>
        public virtual ActionResult UpdateList()
        {
            UpdateListPageModel model = new UpdateListPageModel();

            foreach (string key in this.Request.Form.Keys.Where(s => Guid.TryParse(s, out Guid _)))
            {
                model.Guids.Add(key);
            }

            return this.View(model);
        }

        /// <summary>
        /// Updates the properties of the given object using the provided json
        /// </summary>
        /// <param name="json">The json containing the new property values to apply to the target</param>
        /// <param name="toSave">The target object to update using the provided json</param>
        /// <param name="t">An optional override type to use in place of the target object type</param>
        public void UpdateProperties(string json, KeyedObject toSave, DynamicSaveCache cache, Type? t = null)
        {
            if (toSave is null)
            {
                throw new ArgumentNullException(nameof(toSave));
            }

            if (cache is null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            t ??= toSave.GetType();

            if (t is null)
            {
                throw new Exception("What the fuck?");
            }

            JObject jObject = JObject.Parse(json);

            foreach (PropertyInfo thisProperty in t.GetProperties())
            {
                if (thisProperty.GetGetMethod() == null)
                {
                    continue;
                }

                if (thisProperty.PropertyType == typeof(string))
                {
                    continue;
                }

                if (thisProperty.PropertyType.GetInterface("IEnumerable") != null)
                {
                    //Create an array of the values we're going to use from the source JSON
                    JArray sourceArray = jObject.Property(thisProperty).Value as JArray;

                    //Remove the array source from the JSON so we dont "populate" it later, we're managing that now.
                    //Also, turn that array into a concrete collection so we dont have to do that manually
                    //Were going to treat it as an IEnumerable because we dont actually know what the implementation is on the target. This is the safes way
                    IEnumerable<KeyedObject> sourceEnumerable = jObject.Remove<IEnumerable<KeyedObject>>(thisProperty);

                    //Figure out the base type for the target collection
                    Type[] GenericArguments = thisProperty.PropertyType.GetGenericArguments();

                    //No base type? We cant do anything. Move on;
                    if (!GenericArguments.Any())
                    {
                        continue;
                    }

                    //Now we have the collection base type
                    Type listType = GenericArguments[0];

                    //Turn out input json collection into a list
                    IList tempCollection = sourceEnumerable.ToList();

                    //Create a list to bind to the return object once we've updated everything
                    IList newCollection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));

                    //Grab the collection thats on the object we already have, since it might already have DB instances so we dont have to search

                    //Create a dictionary to hold these existing instances to avoid lookup overhead
                    Dictionary<int, KeyedObject> ExistingDictionary = new Dictionary<int, KeyedObject>();

                    //If we managed to grab the existing object enumerable
                    if (!(!(thisProperty.GetValue(toSave) is IEnumerable existingCollection)))
                    {
                        //Loop through it
                        foreach (KeyedObject ko in existingCollection)
                        {
                            //we cant use null entries
                            if (ko is null)
                            {
                                continue;
                            }

                            //And index the values
                            ExistingDictionary.Add(ko._Id, ko);
                        }
                    }

                    if (listType.IsSubclassOf(typeof(Entity)))
                    {
                        this.UpdateEntityList(tempCollection!);

                        foreach (object o in tempCollection)
                        {
                            newCollection.Add(o);
                        }
                    }
                    //If this collection is KeyedObjects
                    else if (listType.IsSubclassOf(typeof(KeyedObject)))
                    {
                        //Grab an instance of the repository we need now, instead of on every iteration
                        IKeyedObjectRepository repository = this.ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(listType);

                        //Now loop through the temporary object list from the source json
                        for (int i = 0; i < tempCollection.Count; i++)
                        {
                            //Grab the instance
                            KeyedObject toPopulate = (KeyedObject)tempCollection[i];

                            //Set up to grab any existing instance
                            KeyedObject existingObject = null;

                            //If the new json claims the object already exists
                            if (toPopulate._Id != 0)
                            {
                                //We're going to check the object holding the original list to see if it contains that value
                                //If we cant find it here, UpdateJsonObject will check again.
                                ExistingDictionary.Remove(toPopulate._Id, out existingObject);
                            }

                            //We're going to call further down the stack to get an instance attached to the DB and containing all the new values
                            KeyedObject updatedObject = this.UpdateJsonObject(sourceArray[i].ToString(), (KeyedObject)tempCollection[i], cache, listType, repository, existingObject);

                            //Add that proper instance to the list we're using as our new collection value
                            newCollection.Add(updatedObject);
                        }

                        if (!(repository is null))
                        {
                            //Now loop through any of the values that werent found in the new list and remove them from the repository.
                            //Keyed objects are only referenced by one owner so once removed, they have no use.
                            foreach (KeyValuePair<int, KeyedObject> kvp in ExistingDictionary)
                            {
                                KeyedObject toRemove = kvp.Value;

                                repository.Delete(toRemove);
                            }
                        }
                    }

                    //Assign the new collection to the object that was passed in as our target
                    thisProperty.SetValue(toSave, newCollection);
                }
                else if (typeof(Entity).IsAssignableFrom(thisProperty.PropertyType))
                {
                    Entity newValue = jObject.Remove<Entity>(thisProperty);

                    if (!(newValue is null))
                    {
                        Entity cachedEntity = cache.GetObject(newValue);

                        if (cachedEntity is null)
                        {
                            newValue = this.RetrieveSavedEntity(newValue);
                            cache.AddObject(newValue);
                        }
                        else
                        {
                            newValue = cachedEntity;
                        }
                    }

                    thisProperty.SetValue(toSave, newValue);
                }
                else if (thisProperty.PropertyType.IsSubclassOf(typeof(KeyedObject)))
                {
                    string jProp = JObject.Parse(json)[thisProperty.Name]?.ToString();

                    //Remove the value attached to the post since we're going to handle it here
                    jObject.Remove(thisProperty.Name);

                    //Dont fuck with this property if it wasn't sent over by the client!
                    //Missing string, dont touch.
                    if (!(jProp is null))
                    {
                        //EMPTY string is manually set to null
                        if (string.IsNullOrWhiteSpace(jProp))
                        {
                            if (!(!(thisProperty.GetValue(toSave) is KeyedObject existing)))
                            {
                                IKeyedObjectRepository repository = this.ServiceProvider.GetRepositoryForType<IKeyedObjectRepository>(thisProperty.PropertyType);

                                repository.Delete(existing);

                                thisProperty.SetValue(toSave, null);
                            }
                        }
                        //Non empty string needs update
                        else
                        {
                            //Check for existing prop value

                            //if existing val is null, try and create a new one so we have something to set
                            if (!(thisProperty.GetValue(toSave) is KeyedObject val))
                            {
                                val = JsonConvert.DeserializeObject(jProp, thisProperty.PropertyType) as KeyedObject;
                            }

                            //If all that works out we're going to add it to the object
                            if (!(val is null))
                            {
                                KeyedObject thisObject = this.UpdateJsonObject(jProp, val, cache, thisProperty.PropertyType);

                                thisProperty.SetValue(toSave, thisObject);
                            }
                        }
                    }
                }
            }

            JsonConvert.PopulateObject(jObject.ToString(), toSave);
        }

        /// <summary>
        /// Tries to get the _Id property from the provided JObject representing the entity
        /// </summary>
        /// <param name="tempEntity">The JObject to try and get the _Id property from</param>
        /// <param name="Id">The Id int, if found</param>
        /// <returns>True if the Id property is found, false if not</returns>
        private static bool TryGetId(JObject tempEntity, out int Id)
        {
            Id = 0;

            //Should probably be switched to pattern match instead of calling twice
            if (tempEntity.Properties().Any(p => p.Name == nameof(KeyedObject._Id)))
            {
                JToken? property = tempEntity[nameof(KeyedObject._Id)];

                if (property is null)
                {
                    throw new Exception("How did we get here?");
                }

                Id = (int)property;
                return true;
            }

            return false;
        }
    }
}