using Microsoft.AspNetCore.Mvc;
using SaseAccessManager.Services;

namespace SaseAccessManager.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _service;

        public UsersController(UserService service)
        {
            _service = service;
        }

        public record CreateUserRequest(
            string Email,
            string Name,
            string? LastName,
            int DurationDays
        );

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest req)
        {
            var result = await _service.Create(req.Email, req.Name, req.LastName, req.DurationDays);

            if (!result.Success)
                return BadRequest(new { error = result.Error });

            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> List()
            => Ok(await _service.List());

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _service.Remove(id);

            if (!result.Success)
                return NotFound(new { error = result.Error });

            return Ok(new { message = "Usuário removido/processado" });
        }
    }
}
