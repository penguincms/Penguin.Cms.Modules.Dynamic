using Penguin.Cms.Web.Modules;
using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class EntityViewModel<T> where T : class
    {
        public ICollection<ViewModule> Modules { get; }
        public T Target { get; set; }
        public string Type { get; set; } = string.Empty;

        public EntityViewModel(T target, ICollection<ViewModule>? modules = null)
        {
            this.Modules = modules ?? new List<ViewModule>();

            this.Target = target;
        }
    }
}