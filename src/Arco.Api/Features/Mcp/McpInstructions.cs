namespace Arco.Api.Features.Mcp;

public static class McpInstructions
{
    /// <summary>Sent to the client on initialize: what the Arco is and how to use the tools.</summary>
    public const string Text = """
        Arco é um app pessoal de hábitos com progressão. Hábitos têm níveis (dose e versão mínima); cada check-in
        (Done = Feito, Minimum = Mínimo, Missed = Não fiz) dá XP ao jogador. Ciclos de ~6 semanas têm objetivos, e há
        revisão semanal, sequências (streaks), conquistas e estatísticas.

        Comece com ver_hoje: ele traz a data de hoje, os hábitos (com ids) e o que está pendente. Use as ferramentas
        próprias para o dia a dia; para o resto, ler_documentacao_api e chamar_api cobrem toda a API. Os textos para o
        usuário são em português do Brasil, e as datas são YYYY-MM-DD no fuso do usuário.
        """;
}
