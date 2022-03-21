using Microsoft.AspNetCore.Mvc;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin.Controllers
{
    public class EditController : Controller
    {
        public ActionResult MaterialIconSelector(IMetaObject model) => this.View(model);
    }
}