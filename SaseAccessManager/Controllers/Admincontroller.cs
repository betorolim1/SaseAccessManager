using Microsoft.AspNetCore.Mvc;
using SaseAccessManager.Models;
using SaseAccessManager.Services;

namespace SaseAccessManager.Controllers
{
    /// <summary>
    /// Ferramentas administrativas de recuperação.
    /// POST /api/admin/resync-groups?dryRun=true   -> só relatório (padrão)
    /// POST /api/admin/resync-groups?dryRun=false  -> aplica: readiciona grupos faltantes
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly PostgresUserStore _store;
        private readonly ISaseClient _sase;
        private readonly ILogger<AdminController> _logger;

        public AdminController(PostgresUserStore store, ISaseClient sase, ILogger<AdminController> logger)
        {
            _store = store;
            _sase = sase;
            _logger = logger;
        }

        [HttpPost("resync-groups")]
        public async Task<IActionResult> ResyncGroups([FromQuery] bool dryRun = true, CancellationToken ct = default)
        {
            // Fonte da verdade: grupos gravados no banco (DS_GRUPO_ACESSO)
            var localUsers = (await _store.GetAll())
                .Where(u => u.ST_USUARIO == UserStatus.Active)
                .ToList();

            if (localUsers.Count == 0)
                return BadRequest(new { error = "Banco sem usuários ativos. Confira a connection string (está apontando para o banco certo?)." });

            // Estado atual no SASE
            var saseUsers = await _sase.GetAllUsersAsync(ct);
            var groups = await _sase.GetGroupsAsync(ct);

            // GetAllUsersAsync/GetGroupsAsync engolem erro e retornam vazio — não seguir às cegas
            if (saseUsers.Count == 0 || groups.Count == 0)
                return StatusCode(502, new { error = $"SASE retornou {saseUsers.Count} usuários e {groups.Count} grupos. Abortado (falha na API?)." });

            var saseIds = saseUsers
                .Select(s => s.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Fallback por e-mail (carga local sem id_usuario_perimeter)
            var saseByEmail = saseUsers
                .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                .GroupBy(s => s.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var groupById = groups.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);

            var report = new List<object>();
            var notInSase = new List<object>();
            var unknownGroups = new List<object>();
            int alreadyOk = 0, added = 0, failed = 0;

            foreach (var user in localUsers)
            {
                string? perimeterId = null;

                if (!string.IsNullOrWhiteSpace(user.ID_USUARIO_PERIMETER) &&
                    saseIds.Contains(user.ID_USUARIO_PERIMETER))
                    perimeterId = user.ID_USUARIO_PERIMETER;
                else if (saseByEmail.TryGetValue(user.DS_EMAIL.Trim(), out var idByEmail))
                    perimeterId = idByEmail;

                if (perimeterId is null)
                {
                    notInSase.Add(new
                    {
                        user.DS_EMAIL,
                        grupos = (user.DS_GRUPO_ACESSO ?? [])
                            .Select(id => groupById.TryGetValue(id, out var g) ? g.Name : id)
                    });
                    continue;
                }

                foreach (var groupId in user.DS_GRUPO_ACESSO ?? [])
                {
                    if (!groupById.TryGetValue(groupId, out var group))
                    {
                        unknownGroups.Add(new { user.DS_EMAIL, groupId });
                        continue;
                    }

                    if (group.Users.Contains(perimeterId))
                    {
                        alreadyOk++;
                        continue;
                    }

                    if (dryRun)
                    {
                        report.Add(new { user.DS_EMAIL, grupo = group.Name, acao = "seria readicionado" });
                        continue;
                    }

                    var result = await _sase.AddUserToGroup(groupId, perimeterId);

                    if (result.Success)
                    {
                        added++;
                        _logger.LogInformation("Resync: {Email} readicionado ao grupo {Group}", user.DS_EMAIL, group.Name);
                        report.Add(new { user.DS_EMAIL, grupo = group.Name, acao = "readicionado" });
                    }
                    else
                    {
                        failed++;
                        _logger.LogError("Resync: falha ao readicionar {Email} ao grupo {Group}: {Error}", user.DS_EMAIL, group.Name, result.Error);
                        report.Add(new { user.DS_EMAIL, grupo = group.Name, acao = "FALHA", erro = result.Error });
                    }
                }
            }

            return Ok(new
            {
                dryRun,
                usuariosNoBanco = localUsers.Count,
                vinculosJaCorretos = alreadyOk,
                vinculosFaltando = dryRun ? report.Count : added + failed,
                readicionados = added,
                falhas = failed,
                usuariosQueNaoExistemMaisNoSase = notInSase,
                gruposDesconhecidos = unknownGroups,
                detalhes = report
            });
        }
    }
}