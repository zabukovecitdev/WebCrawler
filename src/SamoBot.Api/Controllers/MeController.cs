using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SamoBot.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        return Ok(new { userId = id, name });
    }
}
