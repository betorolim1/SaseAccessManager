using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaseAccessManager.Cache;
using SaseAccessManager.DTOs;
using SaseAccessManager.Models;
using SaseAccessManager.Services;

namespace SaseAccessManager.Pages.Users;

[Authorize]
public class IndexModel : PageModel
{
    private readonly PostgresUserStore _store;
    private readonly UserService _service;
    private readonly ISaseGroupCache _groupCache;

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    [TempData]
    public string? ToastType { get; set; }

    public List<TemporarySaseUser> Users { get; set; } = new();
    public IReadOnlyList<SaseGroupDto> AllGroups { get; private set; } = [];


    public IndexModel(PostgresUserStore store, UserService service, ISaseGroupCache groupCache)
    {
        _store = store;
        _service = service;
        _groupCache = groupCache;
    }

    public async Task OnGet()
    {
        await LoadUsers();
    }

    public async Task<IActionResult> OnPostRemove(string id)
    {
        var result = await _service.Remove(id);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            ErrorMessage = result.Error;

            ToastMessage = result.Error;
            ToastType = "error";

            await LoadUsers();
        }

        ToastMessage = "Usuário removido com sucesso.";
        ToastType = "success";

        return RedirectToPage();
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

    private async Task LoadUsers()
    {
        AllGroups = await _groupCache.GetAsync();

        Users = (await _store.GetAll())
        .OrderBy(u => u.Status == UserStatus.Removed ? 1 : 0)
        .ThenByDescending(u => u.CreatedAt)
        .ToList();
    }
}