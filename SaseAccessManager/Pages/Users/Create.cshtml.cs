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
    public int DurationDays { get; set; } = 30;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public List<string> SelectedGroups { get; set; } = [];

    [BindProperty]
    public bool IsBatch { get; set; }

    [BindProperty]
    public List<string> BatchEmails { get; set; } = [];

    [BindProperty]
    public List<string> BatchNames { get; set; } = [];

    [BindProperty]
    public List<string> BatchLastNames { get; set; } = [];

    public string? Message { get; set; }

    public IReadOnlyList<SaseGroupDto> AvailableGroups { get; private set; } = [];

    [BindProperty]
    public bool IsEdit { get; set; }

    [BindProperty]
    public string? UserId { get; set; }

    [BindProperty]
    public bool ForceImport { get; set; }

    [BindProperty]
    public string? SaseUserIdForImport { get; set; }

    [BindProperty]
    public List<string> ExistingSaseGroupIds { get; set; } = [];

    public bool ShowImportModal { get; set; }

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
        DurationDays = Math.Max(
            1,
            (int)Math.Ceiling((user.ExpiresAt - DateTime.UtcNow).TotalDays)
        );

        SelectedGroups = user.AccessGroups ?? [];

        IsEdit = true;
        UserId = user.Id;

        return Page();
    }

    public async Task<IActionResult> OnPostBatch()
    {
        AvailableGroups = await _groupCache.GetAsync();

        var users = BatchEmails
            .Select((email, i) => (
                Email: email,
                Name: i < BatchNames.Count ? BatchNames[i] : null,
                LastName: i < BatchLastNames.Count ? BatchLastNames[i] : (string?)null
            ))
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .ToList();

        if (users.Count == 0)
        {
            ToastMessage = "Adicione ao menos um usuário.";
            ToastType = "error";
            return RedirectToPage("/Users/Index");
        }

        var result = await _service.CreateBatch(users, DurationDays, SelectedGroups);

        if (result.SuccessCount == 0)
            ToastMessage = $"Nenhum usuário criado. {result.FailCount} falha(s).";
        else if (result.FailCount == 0)
            ToastMessage = $"{result.SuccessCount} usuário(s) criado(s) com sucesso.";
        else
            ToastMessage = $"{result.SuccessCount} criado(s), {result.FailCount} falha(s).";

        ToastType = result.SuccessCount > 0 ? "success" : "error";

        return RedirectToPage("/Users/Index");
    }

    public async Task<IActionResult> OnPost()
    {
        AvailableGroups = await _groupCache.GetAsync();

        if (!ForceImport && !ModelState.IsValid)
            return Page();

        if (IsEdit)
        {
            var groupsResult = await _service.UpdateGroups(Email, SelectedGroups);

            if (!groupsResult.Success)
            {
                ModelState.AddModelError("", groupsResult.Error!);
                return Page();
            }

            var expirationResult = await _service.UpdateExpiration(UserId!, DurationDays);

            if (!expirationResult.Success)
            {
                ModelState.AddModelError("", expirationResult.Error!);
                return Page();
            }

            ToastMessage = "Usuário atualizado com sucesso.";
            ToastType = "success";

            return RedirectToPage("/Users/Index");
        }

        if (ForceImport)
        {
            var import = await _service.ImportExistingUser(
                Email, Name, LastName, DurationDays, SelectedGroups,
                SaseUserIdForImport!, ExistingSaseGroupIds);

            if (!import.Success)
            {
                Message = import.Error;
                return Page();
            }

            ToastMessage = "Usuário importado e sob gerenciamento do sistema.";
            ToastType = "success";

            return RedirectToPage("/Users/Index");
        }

        var create = await _service.Create(
            Email,
            Name,
            LastName,
            DurationDays,
            SelectedGroups);

        if (create.UserAlreadyExistsInSase)
        {
            ShowImportModal = true;
            SaseUserIdForImport = create.ExistingSaseUserId;
            ExistingSaseGroupIds = create.ExistingSaseGroupIds;
            return Page();
        }

        if (!create.Success)
        {
            Message = create.Error;
            return Page();
        }

        ToastMessage = "Usuário criado com sucesso.";
        ToastType = "success";

        return RedirectToPage("/Users/Index");
    }
}