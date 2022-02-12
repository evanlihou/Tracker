using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tracker.Models;

namespace Tracker.Services;

public class PasswordlessLoginTokenProvider<TUser> : DataProtectorTokenProvider<TUser> 
    where TUser : class
{
    public PasswordlessLoginTokenProvider(IDataProtectionProvider dataProtectionProvider, IOptions<PasswordlessLoginTokenProviderOptions> options, ILogger<PasswordlessLoginTokenProvider<TUser>> logger) : base(dataProtectionProvider, options, logger)
    {
    }

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
        var providerInstance = builder.AddTokenProvider(PasswordlessLoginTokenProvider<ApplicationUser>.Name, totpProvider);

        return providerInstance;
    }
}