using Microsoft.AspNetCore.Mvc;

namespace website_CLB_HTSV.Controllers
{
    public class ChatController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
