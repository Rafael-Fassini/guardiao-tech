using Guardiao.Web.Security;
using System.Security.Claims;
using Xunit;

namespace Guardiao.UnitTests.Web;

public class OperationsAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymous_WhenSessionIsEmpty()
    {
        var provider = new OperationsAuthenticationStateProvider(new OperationsSession());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldExposeRole_WhenSessionIsAuthenticated()
    {
        var session = new OperationsSession();
        session.Login("operator.ana", "operator");
        var provider = new OperationsAuthenticationStateProvider(session);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated ?? false);
        Assert.Equal("operator", state.User.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymous_AfterLogout()
    {
        var session = new OperationsSession();
        session.Login("operator.ana", "operator");
        session.Logout();
        var provider = new OperationsAuthenticationStateProvider(session);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
        Assert.Null(state.User.FindFirstValue(ClaimTypes.Role));
    }
}
