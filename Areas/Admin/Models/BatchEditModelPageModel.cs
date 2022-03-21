using Penguin.Cms.Modules.Dynamic.Attributes;
using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Models
{
    public class BatchEditModelPageModel
    {
        public List<string> Guids { get; set; } = new List<string>();

        [ForceDynamic]
        public object? Template { get; set; }
    }
}