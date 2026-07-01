namespace Dynamic.Users.Domain.Enums;

public enum AuthEventType
{
    RegisterStarted = 1,
    RegisterCompleted = 2,
    LoginSucceeded = 3,
    LoginFailed = 4,
    RefreshSucceeded = 5,
    RefreshFailed = 6,
    Logout = 7,
    PasswordChanged = 8,
    ClassicRegisterCreated = 9,
    PasswordResetRequested = 10,
    PasswordResetCompleted = 11,
    ExternalLoginSucceeded = 12,
    ExternalLoginFailed = 13,
    BackofficeCustomerRegistered = 14
}
