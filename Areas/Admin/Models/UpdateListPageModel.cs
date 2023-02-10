using Penguin.Persistence.Abstractions.Attributes.Rendering;
using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class UpdateListPageModel
    {
        [Display(Order = -1000)]
        public List<string> Guids { get; }

        public UpdateListPageModel()
        {
            Guids = new List<string>();
        }
    }
}