using Microsoft.AspNetCore.Mvc;
using Penguin.Reflection.Serialization.Abstractions.Interfaces;

namespace Penguin.Cms.Modules.Dynamic.Controllers
{
    public class RenderController : Controller
    {
        public ActionResult AsCSV(IMetaObject model)
        {
            return View(model);
        }
    }
}