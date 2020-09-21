using Microsoft.AspNetCore.Mvc;

namespace PureMenuScraper.Controllers
{
    [ApiController]
    public class ErrorController : ControllerBase
    {
        [Route("/error")]
        public IActionResult Error() => Problem();
    }
}
