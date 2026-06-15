using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace NotificationAPI.Areas.Admin
{
    public class AdminAreaConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType.Namespace?.Contains(".Areas.Admin") == true)
            {
                controller.RouteValues["area"] = "Admin";
            }
        }
    }
}
