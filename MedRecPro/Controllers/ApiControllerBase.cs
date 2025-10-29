using Microsoft.AspNetCore.Mvc;

/**************************************************************/
/// <summary>
/// Base class for API controllers in the MedRecPro application. This
/// changes the routing based on the build configuration to facilitate
/// path consistency between local development and production environments.
///</summary >
namespace MedRecPro.Controllers
{
    [ApiController]
#if DEBUG
    [Route("api/[controller]")]  // Local development: /api/Settings
#else
    [Route("[controller]")]       // Production with virtual app: /Settings (virtual app adds /api)
#endif
    public abstract class ApiControllerBase : ControllerBase
    {
    }
}