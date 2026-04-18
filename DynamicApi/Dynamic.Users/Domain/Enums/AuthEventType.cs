namespace Dynamic.Users.Domain.Enums;

public enum AuthEventType
{
    Register = 1,
    LoginSucceeded = 2,
    LoginFailed = 3,
    RefreshSucceeded = 4,
    RefreshFailed = 5,
    Logout = 6
}
