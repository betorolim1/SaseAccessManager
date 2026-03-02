using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaseAccessManager.Cache;
using SaseAccessManager.DTOs;
using SaseAccessManager.Services;
using System.ComponentModel.DataAnnotations;

namespace SaseAccessManager.Pages.Users;

public class CreateModel : PageModel
{
    private readonly UserService _service;
    private readonly ISaseGroupCache _groupCache;

    public CreateModel(UserService service, ISaseGroupCache groupCache)
    {
        _service = service;
        _groupCache = groupCache;
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

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public List<string> SelectedGroups { get; set; } = [];

    public string? Message { get; set; }

    public IReadOnlyList<SaseGroupDto> AvailableGroups { get; private set; } = [];

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string LastName { get; set; } = default!;

        [Required]
        public DateTime ExpiresAt { get; set; }
    }

    public async Task OnGet()
    {
        AvailableGroups = await _groupCache.GetAsync();
    }

    public async Task<IActionResult> OnPost()
    {
        AvailableGroups = await _groupCache.GetAsync();

        if (!ModelState.IsValid)
            return Page();

        if (SelectedGroups == null || SelectedGroups.Count == 0)
            SelectedGroups = ["All Users"];

        var result = await _service.Create(Email, Name, LastName, DurationDays, SelectedGroups);

        if (!result.Success)
        {
            AvailableGroups = await _groupCache.GetAsync();
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        return RedirectToPage("/Users/Index");
    }
}