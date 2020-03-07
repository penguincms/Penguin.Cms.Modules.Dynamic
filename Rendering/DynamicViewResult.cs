namespace Penguin.Cms.Modules.Dynamic.Rendering
{
    public class DynamicViewResult : EditorHandlerResult
    {
        public string ViewPath { get; set; }

        public DynamicViewResult(string viewPath)
        {
            this.ViewPath = viewPath;
        }
    }
}