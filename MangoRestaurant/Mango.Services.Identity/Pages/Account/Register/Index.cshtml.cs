using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using IdentityModel;
using Mango.Services.Identity.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Mango.Services.Identity.Pages.Account.Register;

[SecurityHeaders]
[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly IClientStore _clientStore;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEventService _events;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IClientStore clientStore,
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemeProvider,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IEventService events)
    {
        _userManager = userManager;
        this._clientStore = clientStore;
        this._interaction = interaction;
        this._schemeProvider = schemeProvider;
        this._roleManager = roleManager;
        this._signInManager = signInManager;
        this._events = events;

    }

    [BindProperty]
    public RegisterViewModel RegisterVM { get; set; }
    public List<string> RolesVM { get; set; }
    public UserManager<ApplicationUser> _userManager { get; }

    public async Task<IActionResult> OnGetAsync(string returnUrl)
    {
        await BuildModelAsync(returnUrl);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            // Create the user if doesnt exist
            var user = new ApplicationUser
            {
                UserName = RegisterVM.Username,
                Email = RegisterVM.Email,
                EmailConfirmed = true,
                FirstName = RegisterVM.FirstName,
                LastName = RegisterVM.LastName
            };

            var result = await _userManager.CreateAsync(user, RegisterVM.Password);

            if (result.Succeeded)
            {
                if (!_roleManager.RoleExistsAsync(RegisterVM.RoleName).GetAwaiter().GetResult())
                {
                    var userRole = new IdentityRole
                    {
                        Name = RegisterVM.RoleName,
                        NormalizedName = RegisterVM.RoleName,

                    };
                    await _roleManager.CreateAsync(userRole);
                }

                await _userManager.AddToRoleAsync(user, RegisterVM.RoleName);

                await _userManager.AddClaimsAsync(user, new Claim[]{
                            new Claim(JwtClaimTypes.Name, RegisterVM.Username),
                            new Claim(JwtClaimTypes.Email, RegisterVM.Email),
                            new Claim(JwtClaimTypes.FamilyName, RegisterVM.FirstName),
                            new Claim(JwtClaimTypes.GivenName, RegisterVM.LastName),
                            new Claim(JwtClaimTypes.WebSite, "http://"+RegisterVM.Username+".com"),
                            new Claim(JwtClaimTypes.Role,"User") });

                var context = await _interaction.GetAuthorizationContextAsync(RegisterVM.ReturnUrl);
                var loginresult = await _signInManager.PasswordSignInAsync(RegisterVM.Username, RegisterVM.Password, false, lockoutOnFailure: true);

                if (loginresult.Succeeded)
                {
                    var checkuser = await _userManager.FindByNameAsync(RegisterVM.Username);
                    await _events.RaiseAsync(new UserLoginSuccessEvent(checkuser.UserName, checkuser.Id, checkuser.UserName, clientId: context?.Client.ClientId));

                    if (context != null)
                    {
                        if (context.IsNativeClient())
                        {
                            // The client is native, so this change in how to
                            // return the response is for better UX for the end user.
                            return this.LoadingPage(RegisterVM.ReturnUrl);
                        }

                        // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                        return Redirect(RegisterVM.ReturnUrl);
                    }

                    // request for a local page
                    if (Url.IsLocalUrl(RegisterVM.ReturnUrl))
                    {
                        return Redirect(RegisterVM.ReturnUrl);
                    }
                    else if (string.IsNullOrEmpty(RegisterVM.ReturnUrl))
                    {
                        return Redirect("~/");
                    }
                    else
                    {
                        // user might have clicked on a malicious link - should be logged
                        throw new Exception("invalid return URL");
                    }
                }

            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("Register", error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        // repopulate roles
        RolesVM = new List<string>();
        RolesVM.Add("Admin");
        RolesVM.Add("Customer");
        return Page();



    }

    private async Task BuildModelAsync(string returnUrl)
    {
        RegisterVM = new RegisterViewModel
        {
            ReturnUrl = returnUrl
        };

        // build a model so we know what to show on the reg page
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        RolesVM = new List<string>();
        RolesVM.Add("Admin");
        RolesVM.Add("Customer");
        //ViewBag.message = roles;

        if (context?.IdP != null && await _schemeProvider.GetSchemeAsync(context.IdP) != null)
        {
            var local = context.IdP == Duende.IdentityServer.IdentityServerConstants.LocalIdentityProvider;

            //this is to short circuit the UI and only trigger the one external IDP
            RegisterVM.EnableLocalLogin = local;
            RegisterVM.Username = context?.LoginHint;

            if (!local)
            {
                RegisterVM.ExternalProviders = new[] { new ExternalProvider { AuthenticationScheme = context.IdP } };
            }

            return;
        }

        var schemes = await _schemeProvider.GetAllSchemesAsync();

        var providers = schemes
                .Where(x => x.DisplayName != null)
                .Select(x => new ExternalProvider
                {
                    DisplayName = x.DisplayName ?? x.Name,
                    AuthenticationScheme = x.Name
                }).ToList();


        var allowLocal = true;
        if (context?.Client.ClientId != null)
        {
            var client = await _clientStore.FindEnabledClientByIdAsync(context.Client.ClientId);
            if (client != null)
            {
                allowLocal = client.EnableLocalLogin;

                if (client.IdentityProviderRestrictions != null && client.IdentityProviderRestrictions.Any())
                {
                    providers = providers.Where(provider => client.IdentityProviderRestrictions.Contains(provider.AuthenticationScheme)).ToList();
                }
            }
        }

        RegisterVM = new RegisterViewModel
        {
            AllowRememberLogin = AccountOptions.AllowRememberLogin,
            EnableLocalLogin = allowLocal && AccountOptions.AllowLocalLogin,
            ReturnUrl = returnUrl,
            Username = context?.LoginHint,
            ExternalProviders = providers.ToArray()
        };

    }

}

public class RegisterViewModel
{
    [Required]
    public string Username { get; set; }

    [Required]
    //[EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }

    [Required]
    //[DataType(DataType.Password)]
    public string Password { get; set; }

    public string ReturnUrl { get; set; }
    public string RoleName { get; set; }
    public bool AllowRememberLogin { get; set; } = true;
    public bool EnableLocalLogin { get; set; } = true;

    public IEnumerable<ExternalProvider> ExternalProviders { get; set; } = Enumerable.Empty<ExternalProvider>();
    public IEnumerable<ExternalProvider> VisibleExternalProviders => ExternalProviders.Where(x => !String.IsNullOrWhiteSpace(x.DisplayName));

    public bool IsExternalLoginOnly => EnableLocalLogin == false && ExternalProviders?.Count() == 1;
    public string ExternalLoginScheme => IsExternalLoginOnly ? ExternalProviders?.SingleOrDefault()?.AuthenticationScheme : null;

}

public class ExternalProvider
{
    public string DisplayName { get; set; }
    public string AuthenticationScheme { get; set; }
}

public class AccountOptions
{
    public static bool AllowLocalLogin = true;
    public static bool AllowRememberLogin = true;
    public static TimeSpan RememberMeLoginDuration = TimeSpan.FromDays(30);

    public static bool ShowLogoutPrompt = true;
    public static bool AutomaticRedirectAfterSignOut = false;

    public static string InvalidCredentialsErrorMessage = "Invalid username or password";
}
