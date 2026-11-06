using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class FocusRulesDomainTests
{
    [Fact]
    public void MaxMinutes_is_eight_hours() => Assert.Equal(480, FocusRules.MaxMinutes);
}
