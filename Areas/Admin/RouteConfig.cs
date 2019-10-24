using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Penguin.Web.Abstractions.Interfaces;

namespace Penguin.Cms.Modules.Dynamic.Areas.Admin
{
    public class RouteConfig : IRouteConfig
    {
        //the throwaway values are because ASP.NET tried to route anything where the last section of the URL contained a period
        //to a static file. I havent double checked to see if ASP.NET core does the same thing. They might be vestigial
        public void RegisterRoutes(IRouteBuilder routes)
        {
            routes.MapRoute(
                "Admin_Save",
                "Admin/Save",
                new { area = "admin", controller = "Dynamic", action = "Save" }

            );

            routes.MapRoute(
                "Admin_Submit",
                "Admin/Submit/{type}",
                new { area = "admin", controller = "Dynamic", action = "Submit" }

            );

            routes.MapRoute(
                "Admin_Search",
                "Admin/Dynamic/Search/{type}",
                new { area = "admin", controller = "Dynamic", action = "Search" }

            );

            routes.MapRoute(
                "Admin_Edit",
                "Admin/Edit/{type}/{Id?}",
                new { area = "admin", controller = "Dynamic", action = "Edit", knowncontroller = "false" }
            );

            routes.MapRoute(
                "Admin_Edit_B",
                "Admin/Edit/{type}/{Id?}/{throwaway?}",
                new { area = "admin", controller = "Dynamic", action = "Edit", knowncontroller = "false" }
            );

            routes.MapRoute(
                "Admin_BatchCreate",
                "Admin/BatchCreate/{type?}",
                new { area = "admin", controller = "Dynamic", action = "BatchCreate" }

            );

            routes.MapRoute(
                "Admin_BatchEdit",
                "Admin/BatchEdit",
                new { area = "admin", controller = "Dynamic", action = "BatchEdit" }

            );

            routes.MapRoute(
                "Admin_BatchSave",
                "Admin/BatchSave",
                new { area = "admin", controller = "Dynamic", action = "BatchSave" }

            );

            routes.MapRoute(
                "Admin_List",
                "Admin/List/{type}",
                new { area = "admin", controller = "Dynamic", action = "List", knowncontroller = "false" }

            );

            routes.MapRoute(
                "Admin_List_Controller",
                "Admin/{Controller}/List",
                new { area = "admin", controller = "Dynamic", action = "List", knowncontroller = "true" }
            );

            routes.MapRoute(
                "Admin_List_Controller_B",
                "Admin/{Controller}/List/{throwaway?}",
                new { area = "admin", controller = "Dynamic", action = "List", knowncontroller = "true" }
            );

            routes.MapRoute(
                "Admin_Edit_Controller",
                "Admin/{Controller}/Edit/{Id?}",
                new { area = "admin", controller = "Dynamic", action = "Edit", knowncontroller = "true" }

            );

            routes.MapRoute(
                "Admin_Save_Controller",
                "Admin/{controller}/Save",
                new { area = "admin", controller = "Dynamic", action = "Save", knowncontroller = "true" }

            );
        }
    }
}