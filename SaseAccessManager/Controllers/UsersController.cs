using Microsoft.AspNetCore.Mvc;
using SaseAccessManager.Services;

namespace SaseAccessManager.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _service;

        public UsersController(UserService service)
        {
            _service = service;
        }

        public record CreateUserRequest(string Email, string? Name, int DurationDays);

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest req)
        {
            var user = await _service.Create(req.Email, req.Name, req.DurationDays);
            return Ok(user);
        }

        [HttpGet]
        public async Task<IActionResult> List()
            => Ok(await _service.List());

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.Remove(id);

            if (!success)
                return NotFound();

            return Ok(new { message = "Usuário removido/processado" });
        }
    }
}
