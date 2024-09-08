using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Tracker.Services;

public class PasswordlessLoginTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordlessLoginTokenProviderOptions> options,
    ILogger<PasswordlessLoginTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;

public static class PasswordlessLoginTokenProvider
{
    public const string Name = "PasswordlessLoginTokenProvider";
}

public class PasswordlessLoginTokenProviderOptions : DataProtectionTokenProviderOptions
{
}

public static class CustomIdentityBuilderExtensions
{
    public static IdentityBuilder AddPasswordlessLoginTokenProvider(this IdentityBuilder builder)
    {
        var userType = builder.UserType;
        var totpProvider = typeof(PasswordlessLoginTokenProvider<>).MakeGenericType(userType);
        var providerInstance = builder.AddTokenProvider(PasswordlessLoginTokenProvider.Name, totpProvider);

        return providerInstance;
    }
}