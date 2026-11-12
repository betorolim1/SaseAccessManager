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

        /// <summary>
        /// Recria no SASE os usuários ativos do banco que não existem mais lá (por e-mail),
        /// readiciona os grupos e gera o UPDATE de id_usuario_perimeter para PRODUÇÃO.
        /// POST /api/admin/recreate-missing?dryRun=true  -> só relatório (padrão)
        /// POST /api/admin/recreate-missing?dryRun=false -> recria de fato
        /// </summary>
        [HttpPost("recreate-missing")]
        public async Task<IActionResult> RecreateMissing([FromQuery] bool dryRun = true, CancellationToken ct = default)
        {
            var localUsers = (await _store.GetAll())
                .Where(u => u.ST_USUARIO == UserStatus.Active)
                .ToList();

            if (localUsers.Count == 0)
                return BadRequest(new { error = "Banco sem usuários ativos." });

            var saseUsers = await _sase.GetAllUsersAsync(ct);
            var groups = await _sase.GetGroupsAsync(ct);

            if (saseUsers.Count == 0 || groups.Count == 0)
                return StatusCode(502, new { error = $"SASE retornou {saseUsers.Count} usuários e {groups.Count} grupos. Abortado (falha na API?)." });

            var saseEmails = saseUsers
                .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                .Select(s => s.Email.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var groupById = groups.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);

            // Mesmo e-mail duplicado no banco: recria uma vez só
            var missing = localUsers
                .Where(u => !saseEmails.Contains(u.DS_EMAIL.Trim()))
                .GroupBy(u => u.DS_EMAIL.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(u => u.DH_CRIACAO).First())
                .ToList();

            string GroupNames(TemporarySaseUser u) => string.Join(", ",
                (u.DS_GRUPO_ACESSO ?? []).Select(id => groupById.TryGetValue(id, out var g) ? g.Name : id));

            if (dryRun)
            {
                return Ok(new
                {
                    dryRun,
                    usuariosAtivosNoBanco = localUsers.Count,
                    usuariosNoSase = saseUsers.Count,
                    seriamRecriados = missing.Count,
                    usuarios = missing.Select(u => new
                    {
                        u.DS_EMAIL,
                        nome = $"{u.NM_USUARIO} {u.NM_SOBRENOME}".Trim(),
                        idp = UserService.BuildSaseRequest(u).IdpType,
                        grupos = GroupNames(u)
                    })
                });
            }

            var recreated = new List<object>();
            var failures = new List<object>();
            var prodSql = new List<string>
            {
                "-- UPDATE para PRODUÇÃO: novos id_usuario_perimeter dos usuários recriados",
                $"-- Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}",
                "BEGIN;"
            };

            foreach (var user in missing)
            {
                var create = await _sase.CreateUser(UserService.BuildSaseRequest(user));

                if (!create.Success || string.IsNullOrWhiteSpace(create.UserId))
                {
                    var error = create.AlreadyExists ? "SASE diz que já existe (conflito)" : create.Error;
                    _logger.LogError("Recreate: falha ao recriar {Email}: {Error}", user.DS_EMAIL, error);
                    failures.Add(new { user.DS_EMAIL, erro = error });
                    continue;
                }

                var newId = create.UserId!;
                var groupErrors = new List<string>();

                foreach (var groupId in user.DS_GRUPO_ACESSO ?? [])
                {
                    var add = await _sase.AddUserToGroup(groupId, newId);
                    if (!add.Success)
                        groupErrors.Add($"{(groupById.TryGetValue(groupId, out var g) ? g.Name : groupId)}: {add.Error}");
                }

                // Atualiza o banco onde o app está rodando (local)
                user.ID_USUARIO_PERIMETER = newId;
                await _store.Update(user);

                var email = user.DS_EMAIL.Trim().ToLowerInvariant().Replace("'", "''");
                prodSql.Add(
                    $"UPDATE s_usuario_sase SET id_usuario_perimeter = '{newId}' " +
                    $"WHERE lower(ds_email) = '{email}' AND st_usuario = 'Active';");

                _logger.LogInformation("Recreate: {Email} recriado com ID {Id}", user.DS_EMAIL, newId);

                recreated.Add(new
                {
                    user.DS_EMAIL,
                    novoId = newId,
                    grupos = GroupNames(user),
                    falhasGrupos = groupErrors
                });
            }

            prodSql.Add("COMMIT;");

            return Ok(new
            {
                dryRun,
                recriados = recreated.Count,
                falhas = failures.Count,
                usuariosRecriados = recreated,
                usuariosComFalha = failures,
                sqlProducao = string.Join("\n", prodSql)
            });
        }
    }
}