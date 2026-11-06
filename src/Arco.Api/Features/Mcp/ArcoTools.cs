using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Arco.Api.Features.Mcp;

/// <summary>
/// MCP tools: shortcuts for the everyday actions, plus <c>chamar_api</c> and <c>ler_documentacao_api</c>, which
/// reach every other operation of the API. Every tool returns the API's JSON response.
/// </summary>
[McpServerToolType]
public sealed class ArcoTools(ArcoApiClient api)
{
    private const string StatusDescription = "Done (Feito, dose completa), Minimum (Mínimo, versão mínima) ou Missed (Não fiz).";
    private const string DateDescription = "Data no formato YYYY-MM-DD. Omitida = hoje (no fuso configurado). Aceita até 7 dias para trás.";

    // --- Dia a dia ---

    [McpServerTool(Name = "ver_hoje", Title = "Ver o dia", ReadOnly = true)]
    [Description("Tela Hoje: data, jogador (nível e XP), ciclo e objetivos ativos e todos os hábitos com o check-in de hoje, " +
                 "se são devidos hoje, sequência (streak) e sugestões de subir ou voltar de nível. Comece por aqui.")]
    public Task<string> VerHoje(CancellationToken ct) => api.GetAsync("/api/today", ct);

    [McpServerTool(Name = "fazer_checkin", Title = "Fazer check-in", Idempotent = true)]
    [Description("Marca (ou altera) o check-in de um hábito em um dia. Devolve o XP ganho, o jogador e conquistas desbloqueadas.")]
    public async Task<string> FazerCheckIn(
        [Description("Id do hábito (veja ver_hoje ou listar_habitos).")] int habitId,
        [Description(StatusDescription)] string status,
        [Description(DateDescription)] string? data = null,
        [Description("Minutos dedicados (0 a 480, opcional). Omitido = não mexe; 0 = remove os minutos manuais do dia.")] int? minutos = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Put, "/api/checkins",
            new { habitId, date = data ?? await TodayAsync(ct), status, minutes = minutos }, ct);

    [McpServerTool(Name = "registrar_foco", Title = "Registrar foco")]
    [Description("Registra uma sessão de foco (minutos) em um hábito. Não dá XP; vira uma árvore na floresta.")]
    public async Task<string> RegistrarFoco(
        [Description("Id do hábito.")] int habitId,
        [Description("Minutos de foco (1 a 480).")] int minutos,
        [Description(DateDescription)] string? data = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Post, "/api/focus-sessions",
            new { habitId, minutes = minutos, date = data ?? await TodayAsync(ct) }, ct);

    [McpServerTool(Name = "ver_floresta", Title = "Ver floresta", ReadOnly = true)]
    [Description("Sessões de foco de um período, com total de minutos e minutos por hábito.")]
    public async Task<string> VerFloresta(
        [Description("Início, YYYY-MM-DD. Omitido = 30 dias antes de 'ate'.")] string? de = null,
        [Description("Fim, YYYY-MM-DD. Omitido = hoje.")] string? ate = null,
        [Description("Filtrar por hábito (opcional).")] int? habitId = null,
        CancellationToken ct = default)
    {
        var to = ate ?? await TodayAsync(ct);
        var from = de ?? DateOnly.Parse(to).AddDays(-30).ToString("yyyy-MM-dd");
        var q = $"/api/focus-sessions?from={from}&to={to}" + (habitId is null ? "" : $"&habitId={habitId}");
        return await api.GetAsync(q, ct);
    }

    [McpServerTool(Name = "desfazer_checkin", Title = "Desfazer check-in", Destructive = true)]
    [Description("Remove o check-in de um hábito em um dia (e o XP que ele deu).")]
    public async Task<string> DesfazerCheckIn(
        [Description("Id do hábito.")] int habitId,
        [Description(DateDescription)] string? data = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Delete, $"/api/checkins/{habitId}/{data ?? await TodayAsync(ct)}", null, ct);

    // --- Hábitos ---

    [McpServerTool(Name = "listar_habitos", Title = "Listar hábitos", ReadOnly = true)]
    [Description("Lista os hábitos com frequência, nível atual, dose, mínimo, escada planejada e regra de subida.")]
    public Task<string> ListarHabitos(
        [Description("Incluir hábitos arquivados.")] bool incluirArquivados = false,
        CancellationToken ct = default) =>
        api.GetAsync($"/api/habits?includeArchived={(incluirArquivados ? "true" : "false")}", ct);

    [McpServerTool(Name = "ver_habito", Title = "Ver hábito", ReadOnly = true)]
    [Description("Detalhe de um hábito: níveis, escada planejada, check-ins dos últimos 28 dias, consistência, sequência " +
                 "e sugestões de subir ou voltar de nível.")]
    public Task<string> VerHabito([Description("Id do hábito.")] int habitId, CancellationToken ct = default) =>
        api.GetAsync($"/api/habits/{habitId}", ct);

    [McpServerTool(Name = "criar_habito", Title = "Criar hábito")]
    [Description("Cria um hábito no nível 1. Para escada planejada e regra de subida por semanas, use chamar_api " +
                 "(POST /api/habits com plannedLevels: [{ description, minimumDescription }], levelUpWeeks e " +
                 "levelUpDaysPerWeek; veja ler_documentacao_api).")]
    public Task<string> CriarHabito(
        [Description("Nome (até 80 caracteres).")] string nome,
        [Description("Daily (todo dia), SpecificDays (dias da semana) ou WeeklyTarget (N vezes por semana).")] string frequencia,
        [Description("Dose do nível 1: o que conta como Feito (ex.: \"15 min\").")] string dose,
        [Description("Versão mínima para dias difíceis (ex.: \"5 min\").")] string minimo,
        [Description("Emoji opcional.")] string? emoji = null,
        [Description("Para SpecificDays: dias da semana, 0 = domingo … 6 = sábado.")] int[]? diasDaSemana = null,
        [Description("Para WeeklyTarget: vezes por semana (1 a 7).")] int? vezesPorSemana = null,
        [Description("Id de um objetivo do ciclo para ligar o hábito.")] int? objetivoId = null,
        [Description("Horário do lembrete push, HH:mm.")] string? lembrete = null,
        CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, "/api/habits", new
        {
            name = nome,
            emoji,
            goalId = objetivoId,
            frequencyType = frequencia,
            frequencyDays = diasDaSemana,
            timesPerWeek = vezesPorSemana,
            levelDescription = dose,
            minimumDescription = minimo,
            reminderTime = lembrete,
        }, ct);

    [McpServerTool(Name = "subir_nivel", Title = "Subir de nível")]
    [Description("Sobe o hábito para o próximo nível (+50 XP). Sem dose e mínimo, usa o próximo degrau da escada planejada.")]
    public Task<string> SubirNivel(
        [Description("Id do hábito.")] int habitId,
        [Description("Nova dose (opcional se houver escada planejada).")] string? dose = null,
        [Description("Novo mínimo (opcional se houver escada planejada).")] string? minimo = null,
        CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, $"/api/habits/{habitId}/level-up",
            new { levelDescription = dose, minimumDescription = minimo }, ct);

    [McpServerTool(Name = "voltar_nivel", Title = "Voltar um nível", Destructive = true)]
    [Description("Volta o hábito um nível: o nível atual volta para o topo da escada planejada. Não mexe no XP.")]
    public Task<string> VoltarNivel([Description("Id do hábito.")] int habitId, CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, $"/api/habits/{habitId}/level-down", null, ct);

    // --- Ciclo e objetivos ---

    [McpServerTool(Name = "ver_ciclo_atual", Title = "Ver ciclo atual", ReadOnly = true)]
    [Description("Ciclo ativo (ou null) e seus objetivos, com progresso, marcos e hábitos ligados.")]
    public async Task<string> VerCicloAtual(CancellationToken ct = default)
    {
        var cycle = await api.GetJsonOrNullAsync("/api/cycles/current", ct);
        var goals = cycle is null ? new JsonArray() : await api.GetJsonAsync("/api/goals", ct);
        return new JsonObject { ["cycle"] = cycle, ["goals"] = goals }.ToJsonString();
    }

    [McpServerTool(Name = "atualizar_objetivo", Title = "Registrar progresso de objetivo")]
    [Description("Registra o valor atual de um objetivo numérico (ex.: 7 km). Ao atingir a meta, ele é concluído (+150 XP).")]
    public Task<string> AtualizarObjetivo(
        [Description("Id do objetivo (veja ver_ciclo_atual).")] int objetivoId,
        [Description("Valor atual.")] double valor,
        [Description("Nota opcional.")] string? nota = null,
        CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, $"/api/goals/{objetivoId}/progress", new { value = valor, note = nota }, ct);

    [McpServerTool(Name = "marcar_marco", Title = "Marcar ou desmarcar marco")]
    [Description("Alterna um marco de um objetivo de marcos (+25 XP ao concluir; desfazer remove o XP).")]
    public Task<string> MarcarMarco(
        [Description("Id do objetivo.")] int objetivoId,
        [Description("Id do marco.")] int marcoId,
        CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Post, $"/api/goals/{objetivoId}/milestones/{marcoId}/toggle", null, ct);

    // --- Revisão e números ---

    [McpServerTool(Name = "ver_revisao_semanal", Title = "Ver revisão semanal", ReadOnly = true)]
    [Description("Números da semana revisável (XP, check-ins, consistência por hábito, objetivos) e a revisão, se já feita.")]
    public Task<string> VerRevisaoSemanal(CancellationToken ct = default) => api.GetAsync("/api/reviews/weekly/current", ct);

    [McpServerTool(Name = "fazer_revisao_semanal", Title = "Fazer revisão semanal")]
    [Description("Registra a revisão da semana revisável (+30 XP, uma por semana). Pelo menos uma resposta.")]
    public async Task<string> FazerRevisaoSemanal(
        [Description("O que funcionou.")] string? oQueFuncionou = null,
        [Description("O que atrapalhou.")] string? oQueAtrapalhou = null,
        [Description("Foco da próxima semana.")] string? foco = null,
        CancellationToken ct = default)
    {
        var weekStart = (await api.GetJsonAsync("/api/reviews/weekly/current", ct))["weekStart"]?.GetValue<string>();
        return await api.SendAsync(HttpMethod.Post, "/api/reviews/weekly",
            new { weekStart, wins = oQueFuncionou, obstacles = oQueAtrapalhou, next = foco }, ct);
    }

    [McpServerTool(Name = "ver_estatisticas", Title = "Ver estatísticas", ReadOnly = true)]
    [Description("Últimas 12 semanas (consistência e XP por semana) e últimos 30 dias por hábito.")]
    public Task<string> VerEstatisticas(CancellationToken ct = default) => api.GetAsync("/api/stats", ct);

    [McpServerTool(Name = "ver_conquistas", Title = "Ver conquistas", ReadOnly = true)]
    [Description("Todas as conquistas, com progresso e data de desbloqueio.")]
    public Task<string> VerConquistas(CancellationToken ct = default) => api.GetAsync("/api/achievements", ct);

    // --- Cobertura total ---

    [McpServerTool(Name = "ler_documentacao_api", Title = "Ler documentação da API", ReadOnly = true)]
    [Description("Contrato completo da API REST do Arco (rotas, corpos, respostas, regras). Leia antes de usar chamar_api.")]
    public static string LerDocumentacaoApi() => ApiDocs.Markdown;

    [McpServerTool(Name = "chamar_api", Title = "Chamar a API", OpenWorld = false)]
    [Description("Chama qualquer rota da API REST do Arco, para o que não tem ferramenta própria (ciclos, configurações, " +
                 "editar ou arquivar hábito, objetivos, histórico de check-ins, etc.). Consulte ler_documentacao_api.")]
    public Task<string> ChamarApi(
        [Description("GET, POST, PUT ou DELETE.")] string metodo,
        [Description("Rota começando com /api/, com a query string se houver (ex.: /api/checkins?from=2026-10-01&to=2026-10-31).")] string rota,
        [Description("Corpo JSON (texto), para POST e PUT.")] string? corpoJson = null,
        CancellationToken ct = default)
    {
        var method = metodo.Trim().ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new McpException("Método inválido: use GET, POST, PUT ou DELETE."),
        };
        if (corpoJson is not null)
        {
            try
            {
                JsonNode.Parse(corpoJson);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new McpException("corpoJson não é um JSON válido.");
            }
        }
        return api.SendAsync(method, rota.Trim(), corpoJson ?? (method == HttpMethod.Post ? "{}" : null), ct);
    }

    private async Task<string> TodayAsync(CancellationToken ct) =>
        (await api.GetJsonAsync("/api/today", ct))["date"]?.GetValue<string>()
        ?? throw new McpException("Não foi possível obter a data de hoje.");
}

/// <summary>docs/API.md, embedded in the assembly at build time.</summary>
public static class ApiDocs
{
    public static string Markdown { get; } = Load();

    private static string Load()
    {
        using var stream = typeof(ApiDocs).Assembly.GetManifestResourceStream("Arco.Api.docs.API.md");
        if (stream is null) return "Documentação indisponível.";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
