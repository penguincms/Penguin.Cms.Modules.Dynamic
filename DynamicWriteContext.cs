﻿using Penguin.Cms.Core.Extensions;
using Penguin.Cms.Entities;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Persistence.Repositories.Interfaces;
using Penguin.Reflection;
using System;
using Loxifi;

namespace Penguin.Cms.Modules.Dynamic
{
    public class DynamicWriteContext : IDisposable
    {
        public Entity Entity { get; set; }

        public Type EntityType { get; set; }

        public IKeyedObjectRepository TypeRepository { get; set; }

        private IWriteContext WriteContext { get; set; }

        public DynamicWriteContext(string TypeString, int Id, IServiceProvider serviceProvider)
        {
            Type t = TypeFactory.Default.GetTypeByFullName(TypeString, typeof(Entity));

            TypeRepository = serviceProvider.GetRepositoryForType<IKeyedObjectRepository>(t);

            if (TypeRepository is null)
            {
                throw new Exception($"Typed repository not found for type {t}");
            }

            WriteContext = TypeRepository.WriteContext();

            Entity = (Id == 0 ? Activator.CreateInstance(t) : TypeRepository.Find(Id)) is Entity entity
                ? entity
                : throw new Exception($"Unable to find or create entity instance of type {t}");
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);// TODO: uncomment the following line if the finalizer is overridden above.

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    WriteContext.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DynamicWriteContext()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}