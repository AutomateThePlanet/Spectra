namespace TestAppAutomation.Models;

/// <summary>
/// Shared test data constants for automation tests.
/// </summary>
public static class TestData
{
    public const string ValidUsername = "testuser@example.com";
    public const string ValidPassword = "SecureP@ss123!";
    public const string InvalidPassword = "wrong";
    public const string LockedUsername = "locked@example.com";
    public const string AdminUsername = "admin@example.com";

    public const string TestCardNumber = "4111111111111111";
    public const string TestCardExpiry = "12/28";
    public const string TestCardCvv = "123";

    public const decimal StandardFee = 50.00m;
    public const decimal ExpeditedFee = 25.00m;

    public const string BaseUrl = "https://test.example.gov";
    public const string ApiBaseUrl = "https://api.test.example.gov";
}
