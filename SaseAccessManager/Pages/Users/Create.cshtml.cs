using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaseAccessManager.Cache;
using SaseAccessManager.DTOs;
using SaseAccessManager.Services;
using System.ComponentModel.DataAnnotations;

namespace SaseAccessManager.Pages.Users;

[Authorize]
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
    [Display(Name = "Email")]
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de email inválido.")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Display(Name = "Nome")]
    [Required(ErrorMessage = "Nome é obrigatório.")]
    public string Name { get; set; } = "";

    [BindProperty]
    [Display(Name = "Sobrenome")]
    [Required(ErrorMessage = "Sobrenome é obrigatório.")]
    public string LastName { get; set; } = "";

    [BindProperty]
    [Display(Name = "Validade (dias)")]
    [Required(ErrorMessage = "Duração é obrigatória.")]
    [Range(1, 365, ErrorMessage = "Duração deve ser entre 1 e 365 dias.")]
    public int DurationDays { get; set; } = 15;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public List<string> SelectedGroups { get; set; } = [];

    public string? Message { get; set; }

    public IReadOnlyList<SaseGroupDto> AvailableGroups { get; private set; } = [];

    [BindProperty]
    public bool IsEdit { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    [TempData]
    public string? ToastType { get; set; } // success | error

    public class InputModel
    {
        [Display(Name = "Email")]
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Display(Name = "Nome")]
        [Required]
        public string Name { get; set; } = default!;

        [Display(Name = "Sobrenome")]
        [Required]
        public string LastName { get; set; } = default!;

        [Display(Name = "Expira em")]
        [Required]
        public DateTime ExpiresAt { get; set; }
    }

    public IActionResult OnPostLogout()
    {
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    public async Task<IActionResult> OnGet(string? id)
    {
        AvailableGroups = await _groupCache.GetAsync();

        if (string.IsNullOrWhiteSpace(id))
            return Page();

        var users = await _service.List();

        var user = users.FirstOrDefault(u => u.Id == id);

        if (user == null)
            return RedirectToPage("/Users/Index");

        Email = user.Email;
        Name = user.Name ?? "";
        LastName = user.LastName ?? "";
        DurationDays = (int)(user.ExpiresAt - DateTime.UtcNow).TotalDays;

        SelectedGroups = user.AccessGroups ?? [];

        IsEdit = true;

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        AvailableGroups = await _groupCache.GetAsync();

        if (!ModelState.IsValid)
            return Page();

        if (IsEdit)
        {
            var result = await _service.UpdateGroups(Email, SelectedGroups);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Error!);
                return Page();
            }

            ToastMessage = "Grupos atualizados com sucesso.";
            ToastType = "success";

            return RedirectToPage("/Users/Index");
        }

        var create = await _service.Create(
            Email,
            Name,
            LastName,
            DurationDays,
            SelectedGroups);

        if (!create.Success)
        {
            ModelState.AddModelError("", create.Error!);
            return Page();
        }

        ToastMessage = "Usuário criado com sucesso.";
        ToastType = "success";

        return RedirectToPage("/Users/Index");
    }
}