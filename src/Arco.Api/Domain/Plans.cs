namespace Arco.Api.Domain;

/// <summary>What each plan allows. Free is limited; Pro is unlimited and has everything.</summary>
public static class PlanLimits
{
    public static int? MaxActiveHabits(UserPlan p) => p == UserPlan.Free ? 5 : null;
    public static int? MaxApiKeys(UserPlan p) => p == UserPlan.Free ? 1 : null;
    public static bool Mcp(UserPlan p) => p == UserPlan.Pro;
    public static bool ProfileTheme(UserPlan p) => p == UserPlan.Pro;
}
