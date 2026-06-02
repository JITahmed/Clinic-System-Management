using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using ClinicSystem.Reporting.DTOs;
using System.Text.Json;

namespace ClinicSystem.Reporting.Controllers
{
    public class AuthorizeReportingAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var jwt = context.HttpContext.Session.GetString("jwt");
            if (string.IsNullOrEmpty(jwt))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
            base.OnActionExecuting(context);
        }
    }
}
