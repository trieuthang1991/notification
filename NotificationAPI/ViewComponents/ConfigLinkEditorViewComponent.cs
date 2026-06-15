using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NotificationAPI.Enums;
using NotificationAPI.Models;
using System.Collections.Generic;
using System.Linq;

namespace NotificationAPI.ViewComponents
{
    public class ConfigLinkEditorViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(List<ConfigLink> configLinks, string fieldName, string title = "Cấu hình liên kết", bool isRequired = false)
        {
            var model = new ConfigLinkEditorViewModel
            {
                ConfigLinks = configLinks ?? new List<ConfigLink>(),
                FieldName = fieldName,
                Title = title,
                IsRequired = isRequired,
                LinkTypeOptions = GetLinkTypeOptions(),
                LinkTypeGetOptions = GetLinkTypeGetOptions()
            };

            return View(model);
        }

        private List<SelectListItem> GetLinkTypeOptions()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = ConfigLinkType.Path.ToString("d"), Text = "Chỉ Path" },
                new SelectListItem { Value = ConfigLinkType.Link.ToString("d"), Text = "Path + Query" }
            };
        }

        private List<SelectListItem> GetLinkTypeGetOptions()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = ConfigLinkTypeGet.Same.ToString("d"), Text = "So sánh chính xác" },
                new SelectListItem { Value = ConfigLinkTypeGet.Regex.ToString("d"), Text = "So sánh Regex" }
            };
        }
    }

    public class ConfigLinkEditorViewModel
    {
        public List<ConfigLink> ConfigLinks { get; set; } = new List<ConfigLink>();
        public string FieldName { get; set; } = string.Empty;
        public string Title { get; set; } = "Cấu hình liên kết";
        public bool IsRequired { get; set; } = false;
        public List<SelectListItem> LinkTypeOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> LinkTypeGetOptions { get; set; } = new List<SelectListItem>();
    }
}
