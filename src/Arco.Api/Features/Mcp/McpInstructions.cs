namespace Arco.Api.Features.Mcp;

public static class McpInstructions
{
    /// <summary>Sent to the client on initialize: what the Arco is and how to use the tools.</summary>
    public const string Text = """
        Arco é um app pessoal de hábitos com progressão. Hábitos têm níveis (dose e versão mínima); cada check-in
        (Done = Feito, Minimum = Mínimo, Missed = Não fiz, Skipped = Pulei) dá XP ao jogador. Ciclos de ~6 semanas têm objetivos, e há
        pausas e modo férias (dias dispensados não quebram a sequência), revisão semanal, sequências (streaks), conquistas e estatísticas.

        Cada hábito tem um período (manhã, tarde, noite ou a qualquer hora) e pode vir "depois de" outro hábito ou de um texto;
        o Hoje mostra os hábitos nessa ordem do dia (criar_habito e editar_habito definem período e âncora).

        Comece com ver_hoje: ele traz a data de hoje, os hábitos (com ids) e o que está pendente. Use as ferramentas
        próprias para o dia a dia; para o resto, ler_documentacao_api e chamar_api cobrem toda a API. Os textos para o
        usuário são em português do Brasil, e as datas são YYYY-MM-DD no fuso do usuário.
        """;
}
