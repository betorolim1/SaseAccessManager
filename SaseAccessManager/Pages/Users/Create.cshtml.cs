using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaseAccessManager.Services;
using System.ComponentModel.DataAnnotations;

namespace SaseAccessManager.Pages.Users;

public class CreateModel : PageModel
{
    private readonly UserService _service;

    public CreateModel(UserService service)
    {
        _service = service;
    }

    [BindProperty]
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de email inválido.")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Nome é obrigatório.")]
    public string Name { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Sobrenome é obrigatório.")]
    public string LastName { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Duração é obrigatória.")]
    [Range(1, 365, ErrorMessage = "Duração deve ser entre 1 e 365 dias.")]
    public int DurationDays { get; set; } = 7;

    public string? Message { get; set; }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _service.Create(Email, Name, LastName, DurationDays);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        return RedirectToPage("/Users/Index");
    }
}