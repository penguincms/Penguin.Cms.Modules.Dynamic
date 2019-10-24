using Penguin.Reflection.Serialization.Abstractions.Interfaces;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class EditModelPageModel : EntityViewModel<IMetaObject>
    {
        public EditModelPageModel(IMetaObject model) : base(model)
        {
        }
    }
}