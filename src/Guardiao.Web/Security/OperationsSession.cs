namespace Guardiao.Web.Security;

public sealed class OperationsSession
{
    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;

    public void Login(string userName, string role)
    {
        IsAuthenticated = true;
        UserName = userName;
        Role = role;
    }

    public void Logout()
    {
        IsAuthenticated = false;
        UserName = string.Empty;
        Role = string.Empty;
    }
}
