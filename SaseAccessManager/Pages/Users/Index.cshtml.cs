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
    private readonly SaseSyncService _sync;
    private readonly ILogger<IndexModel> _logger;

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    [TempData]
    public string? ToastType { get; set; }

    public List<TemporarySaseUser> Users { get; set; } = new();
    public IReadOnlyList<SaseGroupDto> AllGroups { get; private set; } = [];


    public IndexModel(
        PostgresUserStore store, UserService service, ISaseGroupCache groupCache,
        SaseSyncService sync, ILogger<IndexModel> logger)
    {
        _store = store;
        _service = service;
        _groupCache = groupCache;
        _sync = sync;
        _logger = logger;
    }

    /// <summary>
    /// GET /Users?handler=Orphans — lista os órfãos (no SASE, mas não ativos no SAM) para o modal de sincronização.
    /// </summary>
    public async Task<IActionResult> OnGetOrphans(CancellationToken ct)
    {
        try
        {
            var report = await _sync.FindOrphansAsync(ct);

            return new JsonResult(new
            {
                orphans = report.ToRemove
                    .OrderBy(o => o.Email, StringComparer.OrdinalIgnoreCase)
                    .Select(o => new { id = o.Id, email = o.Email }),
                admins = report.Admins.Count,
                whitelisted = report.Whitelisted.Count,
                saseTotal = report.SaseCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuários órfãos para sincronização manual.");

            // Mensagem amigável; o detalhe técnico fica no log
            var msg = ex is InvalidOperationException
                ? ex.Message
                : "Não foi possível consultar o SASE agora. Veja os logs para detalhes.";

            return new JsonResult(new { error = msg }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Confirmação da sincronização manual. Os órfãos são recalculados aqui:
    /// só é removido quem estava na lista mostrada E continua órfão agora.
    /// </summary>
    public async Task<IActionResult> OnPostSyncOrphans(List<string>? ids)
    {
        var quem = User.Identity?.Name ?? "usuário desconhecido";
        var selecionados = (ids ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selecionados.Count == 0)
        {
            ToastMessage = "Nenhum usuário selecionado para remoção.";
            ToastType = "error";
            return RedirectToPage();
        }

        try
        {
            var report = await _sync.FindOrphansAsync();

            var paraRemover = report.ToRemove
                .Where(o => selecionados.Contains(o.Id))
                .ToList();

            var ignorados = selecionados.Count - paraRemover.Count;

            _logger.LogWarning(
                "Sincronização manual iniciada por {Quem}: {Count} órfão(s) a remover ({Ignorados} da lista original já não eram órfãos).",
                quem, paraRemover.Count, ignorados);

            var (removed, errors, failures) = await _sync.RemoveOrphansAsync(paraRemover, $"manual por {quem}");

            if (errors == 0)
            {
                ToastMessage = $"Sincronização concluída: {removed} usuário(s) removido(s) do SASE."
                    + (ignorados > 0 ? $" {ignorados} ignorado(s) por não serem mais órfãos." : "");
                ToastType = "success";
            }
            else
            {
                ToastMessage = $"{removed} removido(s), {errors} falha(s):\n" + string.Join("\n", failures.Take(10).Select(f => "• " + f));
                ToastType = "error";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na sincronização manual iniciada por {Quem}.", quem);
            ToastMessage = ex is InvalidOperationException
                ? ex.Message
                : "Erro ao executar a sincronização. Veja os logs para detalhes.";
            ToastType = "error";
        }

        return RedirectToPage();
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