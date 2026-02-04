using Microsoft.AspNetCore.Mvc;

namespace DRaaS.WebApi.Controllers;

public class DrasiInstanceController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
