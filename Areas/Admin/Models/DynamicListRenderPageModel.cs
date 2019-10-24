using Penguin.Cms.Modules.Core.Models;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;
using Penguin.Reflection.Serialization.Abstractions.Wrappers;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class DynamicListRenderPageModel
    {
        public PagedListContainer<IMetaObject> PagedList { get; set; } = new PagedListContainer<IMetaObject>();

        public string Type { get; set; } = string.Empty;

        public static DynamicListRenderPageModel FromPagedList<T>(PagedListContainer<T> Objects)
        {
            if (Objects is null)
            {
                throw new System.ArgumentNullException(nameof(Objects));
            }

            DynamicListRenderPageModel model = new DynamicListRenderPageModel()
            {
                PagedList = new PagedListContainer<IMetaObject>()
                {
                    Count = Objects.Count,
                    Page = Objects.Page,
                    TotalCount = Objects.TotalCount
                },

                Type = nameof(T)
            };

            model.PagedList.HiddenColumns.AddRange(Objects.HiddenColumns);

            foreach (object? thisItem in Objects.Items)
            {
                if (thisItem is IMetaObject m)
                {
                    model.PagedList.Items.Add(m);
                }
                else
                {
                    model.PagedList.Items.Add(new MetaObjectHolder(thisItem));
                }
            }

            return model;
        }
    }
}