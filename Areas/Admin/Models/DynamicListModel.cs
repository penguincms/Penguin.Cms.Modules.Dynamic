using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class DynamicListModel<T>
    {
        public List<string> HiddenColumns { get; } = new List<string>();
        public IEnumerable<T> List { get; set; }

        public DynamicListModel(IEnumerable<T> toDisplay)
        {
            this.List = toDisplay;
        }

        public DynamicListModel(IEnumerable<T> toDisplay, List<string> hiddenColumns)
        {
            HiddenColumns = hiddenColumns;
            this.List = toDisplay;
        }
    }
}