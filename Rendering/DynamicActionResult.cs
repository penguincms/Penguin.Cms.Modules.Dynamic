using System.Collections.Generic;

namespace Penguin.Cms.Modules.Dynamic.Rendering
{
    public class DynamicActionResult : EditorHandlerResult
    {
        public Dictionary<string, object> RouteData { get; set; }

        public DynamicActionResult(Dictionary<string, object> routeData)
        {
            RouteData = routeData;
        }
    }
}