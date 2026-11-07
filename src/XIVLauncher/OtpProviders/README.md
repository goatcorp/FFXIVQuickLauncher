# OTP Providers

This module allows loading external OTP providers as plugins.

## Rules

To be loaded, a provider **must**:
- Implement `IOtpProvider`.
- Have a unique, stable `Id`.
- Have a `DisplayName`.
- Implement `GetOtpAsync(...)`.
- Return a valid `OtpProviderResult`.
- **Return exactly 6-digit numeric codes.** Any other format is rejected.

## Implementation

Provider-specific logic (auth, storage, config) stays inside your assembly. XIVLauncher only handles the final OTP code. We don't know, and we don't care, how you generate it.

```csharp
public sealed class ExampleOtpProvider : IOtpProvider
{
    public string Id => "example";

    public string DisplayName => "Example Provider";

    public Task<OtpProviderResult> GetOtpAsync(
        CancellationToken cancellationToken = default)
    {
        // Return a valid 6-digit code
        return Task.FromResult(new OtpProviderResult
        {
            Success = true,
            Otp = "123456",
        });
    }
}