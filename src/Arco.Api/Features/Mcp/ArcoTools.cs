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
    private const string StatusDescription = "Done (Feito, dose completa), Minimum (Mínimo, versão mínima) Missed (Não fiz) ou Skipped (Pulei: dispensa o dia sem XP e sem quebrar a sequência).";
    private const string DateDescription = "Data no formato YYYY-MM-DD. Omitida = hoje (no fuso configurado). Aceita até 7 dias para trás.";

    private const string PeriodDescription = "Período do dia: manha, tarde, noite ou qualquer (a qualquer hora, o padrão).";
    private const string AfterDescription = "Âncora \"depois de…\": o id de outro hábito (só dígitos) ou um texto livre (até 60 caracteres, ex.: \"o café\").";

    // --- Dia a dia ---

    [McpServerTool(Name = "ver_hoje", Title = "Ver o dia", ReadOnly = true)]
    [Description("Tela Hoje: data, jogador (nível e XP), ciclo e objetivos ativos e todos os hábitos com o check-in de hoje, " +
                 "se são devidos hoje, sequência (streak) e sugestões de subir ou voltar de nível. Os hábitos vêm na ordem do dia " +
                 "(período, ordem manual e encadeamento), com o período atual em currentTimeOfDay e, em cada hábito, a âncora " +
                 "\"depois de…\" (anchorName) e se a vez dele chegou (anchorDone). Comece por aqui.")]
    public Task<string> VerHoje(CancellationToken ct) => api.GetAsync("/api/today", ct);

    [McpServerTool(Name = "fazer_checkin", Title = "Fazer check-in", Idempotent = true)]
    [Description("Marca (ou altera) o check-in de um hábito em um dia. Devolve o XP ganho, o jogador e conquistas desbloqueadas.")]
    public async Task<string> FazerCheckIn(
        [Description("Id do hábito (veja ver_hoje ou listar_habitos).")] int habitId,
        [Description(StatusDescription)] string status,
        [Description(DateDescription)] string? data = null,
        [Description("Minutos dedicados (0 a 480, opcional). Omitido = não mexe; 0 = remove os minutos manuais do dia. Não vale com Missed nem Skipped.")] int? minutos = null,
        [Description("Nota opcional (até 280 caracteres), em qualquer status. Omitida = mantém a nota já salva; texto vazio (\"\") = remove a nota; texto = substitui.")] string? nota = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Put, "/api/checkins",
            new { habitId, date = data ?? await TodayAsync(ct), status, minutes = minutos, note = nota }, ct);

    [McpServerTool(Name = "registrar_humor", Title = "Registrar humor", Idempotent = true)]
    [Description("Registra (ou altera) o humor do dia, de 1 (muito ruim) a 5 (muito bom). Um por dia; não dá XP. " +
                 "Alimenta a revisão semanal (humor médio e consistência em dias bons e ruins).")]
    public async Task<string> RegistrarHumor(
        [Description("Humor de 1 (muito ruim) a 5 (muito bom).")] int valor,
        [Description(DateDescription)] string? data = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Put, "/api/moods",
            new { date = data ?? await TodayAsync(ct), value = valor }, ct);

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

    // --- Pausas ---

    [McpServerTool(Name = "pausar_habito", Title = "Pausar hábito")]
    [Description("Pausa um hábito por um período: os dias pausados não quebram a sequência nem contam como falha, e não dão XP. " +
                 "Sem data de fim, a pausa dura até retomar_habito.")]
    public async Task<string> PausarHabito(
        [Description("Id do hábito.")] int habitId,
        [Description("Início, YYYY-MM-DD. Omitido = hoje. No máximo 7 dias para trás.")] string? inicio = null,
        [Description("Fim (incluso), YYYY-MM-DD, até 365 dias após o início. Omitido = até retomar.")] string? fim = null,
        [Description("Motivo (opcional, até 140 caracteres).")] string? motivo = null,
        CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Post, $"/api/habits/{habitId}/pauses",
            new { startDate = inicio ?? await TodayAsync(ct), endDate = fim, reason = motivo }, ct);

    [McpServerTool(Name = "retomar_habito", Title = "Retomar hábito")]
    [Description("Retoma um hábito pausado: encerra a pausa em curso (ela termina ontem, ou é apagada se começou hoje).")]
    public async Task<string> RetomarHabito([Description("Id do hábito.")] int habitId, CancellationToken ct = default) =>
        await api.SendAsync(HttpMethod.Post, $"/api/habits/{habitId}/resume", null, ct);

    [McpServerTool(Name = "modo_ferias", Title = "Modo férias")]
    [Description("Modo férias: 'ativar' pausa todos os hábitos ativos (sem pausa sobreposta) de inicio a fim; " +
                 "'encerrar' retoma as pausas de férias em curso e apaga as futuras. Sem ação, mostra o estado atual.")]
    public async Task<string> ModoFerias(
        [Description("'ativar', 'encerrar' ou omitido para ver o estado atual.")] string? acao = null,
        [Description("Início das férias, YYYY-MM-DD (ativar). Omitido = hoje.")] string? inicio = null,
        [Description("Fim das férias, YYYY-MM-DD (ativar, obrigatório).")] string? fim = null,
        [Description("Motivo (opcional, ativar).")] string? motivo = null,
        CancellationToken ct = default)
    {
        switch (acao?.Trim().ToLowerInvariant())
        {
            case null or "":
                return await api.GetAsync("/api/vacation", ct);
            case "ativar":
                if (string.IsNullOrWhiteSpace(fim)) throw new McpException("Informe 'fim' (YYYY-MM-DD) para ativar o modo férias.");
                return await api.SendAsync(HttpMethod.Post, "/api/vacation",
                    new { startDate = inicio ?? await TodayAsync(ct), endDate = fim, reason = motivo }, ct);
            case "encerrar":
                return await api.SendAsync(HttpMethod.Delete, "/api/vacation", null, ct);
            default:
                throw new McpException("Ação inválida: use 'ativar' ou 'encerrar'.");
        }
    }

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
        [Description(PeriodDescription)] string? periodo = null,
        [Description(AfterDescription)] string? depoisDe = null,
        CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["name"] = nome,
            ["emoji"] = emoji,
            ["goalId"] = objetivoId,
            ["frequencyType"] = frequencia,
            ["frequencyDays"] = diasDaSemana is null ? null : new JsonArray(diasDaSemana.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray()),
            ["timesPerWeek"] = vezesPorSemana,
            ["levelDescription"] = dose,
            ["minimumDescription"] = minimo,
            ["reminderTime"] = lembrete,
        };
        if (periodo is not null) body["timeOfDay"] = ParsePeriod(periodo);
        if (!string.IsNullOrWhiteSpace(depoisDe)) ApplyAnchor(body, depoisDe);
        return api.SendAsync(HttpMethod.Post, "/api/habits", body, ct);
    }

    [McpServerTool(Name = "editar_habito", Title = "Editar hábito")]
    [Description("Edita o período do dia, a âncora \"depois de…\" e/ou o nome de um hábito, mantendo todo o resto. " +
                 "Só o que for informado muda. Para outros campos, use chamar_api (PUT /api/habits/{id}).")]
    public async Task<string> EditarHabito(
        [Description("Id do hábito.")] int habitoId,
        [Description(PeriodDescription)] string? periodo = null,
        [Description(AfterDescription + " Texto vazio (\"\") remove a âncora.")] string? depoisDe = null,
        [Description("Novo nome (até 80 caracteres).")] string? nome = null,
        CancellationToken ct = default)
    {
        var habit = await api.GetJsonAsync($"/api/habits/{habitoId}", ct);
        // PUT takes the whole habit: copy what the GET returned, then apply only what was informed.
        var body = new JsonObject();
        foreach (var key in new[]
                 {
                     "name", "emoji", "goalId", "frequencyType", "frequencyDays", "timesPerWeek", "levelDescription",
                     "minimumDescription", "reminderTime", "plannedLevels", "levelUpWeeks", "levelUpDaysPerWeek",
                 })
            body[key] = habit[key]?.DeepClone();
        if (nome is not null) body["name"] = nome;
        if (periodo is not null) body["timeOfDay"] = ParsePeriod(periodo);
        if (depoisDe is not null) ApplyAnchor(body, depoisDe);
        return await api.SendAsync(HttpMethod.Put, $"/api/habits/{habitoId}", body, ct);
    }

    private static string ParsePeriod(string periodo) => periodo.Trim().ToLowerInvariant() switch
    {
        "manha" or "manhã" => "Morning",
        "tarde" => "Afternoon",
        "noite" => "Evening",
        "qualquer" => "Anytime",
        _ => throw new McpException("Período inválido: use manha, tarde, noite ou qualquer."),
    };

    private static void ApplyAnchor(JsonObject body, string depoisDe)
    {
        var value = depoisDe.Trim();
        if (int.TryParse(value, out var id)) body["anchorHabitId"] = id;
        else body["anchorText"] = value;
    }

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

    [McpServerTool(Name = "ver_arco", Title = "Ver arco do ciclo", ReadOnly = true)]
    [Description("Arco de foco do ciclo: uma pedra por semana, com minutos por hábito e por dia (tijolos). Sem id, usa o ciclo ativo.")]
    public async Task<string> VerArco(
        [Description("Id do ciclo (opcional; omitido = ciclo ativo).")] int? cicloId = null,
        CancellationToken ct = default)
    {
        if (cicloId is null)
        {
            var cycle = await api.GetJsonOrNullAsync("/api/cycles/current", ct);
            if (cycle is null) return new JsonObject { ["arch"] = null }.ToJsonString();
            cicloId = (int)cycle["id"]!;
        }
        return await api.GetAsync($"/api/cycles/{cicloId}/arch", ct);
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

    // --- Parceiros, perfil público e plano ---

    [McpServerTool(Name = "ver_parceiros", Title = "Ver parceiros", ReadOnly = true)]
    [Description("Parceiros de responsabilidade: com quem você compartilha, quem compartilha com você (ids para ver_resumo_parceiro) e convites pendentes.")]
    public Task<string> VerParceiros(CancellationToken ct = default) => api.GetAsync("/api/partners", ct);

    [McpServerTool(Name = "ver_resumo_parceiro", Title = "Ver resumo do parceiro", ReadOnly = true)]
    [Description("Resumo semanal (semana atual e anterior) de alguém que compartilha com você.")]
    public Task<string> VerResumoParceiro(
        [Description("Id da parceria (veja ver_parceiros, sharedWithMe).")] int id,
        CancellationToken ct = default) =>
        api.GetAsync($"/api/partners/{id}/summary", ct);

    [McpServerTool(Name = "configurar_perfil_publico", Title = "Configurar perfil público")]
    [Description("Liga ou desliga o perfil público e escolhe o endereço (slug). Sem argumentos, só devolve o estado atual e o link.")]
    public Task<string> ConfigurarPerfilPublico(
        [Description("true liga, false desliga. Omitido = não mexe.")] bool? ativo = null,
        [Description("Endereço: 3 a 30 caracteres, letras minúsculas, números e hífen. Omitido = não mexe.")] string? slug = null,
        CancellationToken ct = default) =>
        api.SendAsync(HttpMethod.Put, "/api/profile/public", new { enabled = ativo, slug }, ct);

    [McpServerTool(Name = "ver_plano", Title = "Ver plano", ReadOnly = true)]
    [Description("Plano atual (free ou pro), renovação, limites e uso.")]
    public Task<string> VerPlano(CancellationToken ct = default) => api.GetAsync("/api/billing", ct);

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
