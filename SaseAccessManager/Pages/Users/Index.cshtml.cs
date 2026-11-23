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

#if !DEBUG
[Authorize]
#endif
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

    public async Task<IActionResult> OnPostRemove(Guid id, string? observacao)
    {
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        var dataBrasilia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brasilia);

        var texto = $"{dataBrasilia:dd/MM/yyyy HH:mm} Horário de Brasília";

        var result = await _service.Remove(id, $"Removido manualmente em {texto}", observacao);

        if (!result.Success)
        {
            ToastMessage = result.Error;
            ToastType = "error";
            return RedirectToPage();
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

    public async Task<IActionResult> OnPostReactivate(
        Guid id, int durationDays = 30, string? name = null, string? lastName = null,
        List<string>? groups = null, bool groupsProvided = false)
    {
        List<string>? selectedGroups = null;

        if (groupsProvided)
        {
            // Só aceita IDs de grupos que existem no SASE
            var validIds = (await _groupCache.GetAsync())
                .Select(g => g.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Sem a lista de grupos do SASE não dá para validar: não reativa sem grupos por engano
            if (validIds.Count == 0)
            {
                ToastMessage = "Não foi possível carregar os grupos do SASE. Tente novamente em instantes.";
                ToastType = "error";
                return RedirectToPage();
            }

            selectedGroups = (groups ?? [])
                .Where(validIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var result = await _service.Reactivate(id, durationDays, name, lastName, selectedGroups);

        if (!result.Success)
        {
            ToastMessage = result.Error;
            ToastType = "error";
            return RedirectToPage();
        }

        ToastMessage = "Usuário reativado com sucesso.";
        ToastType = "success";

        return RedirectToPage();
    }

    private async Task LoadUsers()
    {
        AllGroups = await _groupCache.GetAsync();

        Users = (await _store.GetAll())
        .OrderBy(u => u.ST_USUARIO == UserStatus.Removed ? 1 : 0)
        .ThenByDescending(u => u.DH_CRIACAO)
        .ToList();
    }
}