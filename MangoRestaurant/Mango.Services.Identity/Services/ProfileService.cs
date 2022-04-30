using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityModel;
using Mango.Services.Identity.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Mango.Services.Identity.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserClaimsPrincipalFactory<ApplicationUser> _userClaimsPrincipalFactory;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserClaimsPrincipalFactory<ApplicationUser> userClaimsPrincipalFactory)
        {
            this._userManager = userManager;
            this._roleManager = roleManager;
            this._userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        }
        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            string sub = context.Subject.GetSubjectId(); // in the subject you find the userId of the user
            ApplicationUser user = await _userManager.FindByIdAsync(sub); //Get complete user
            ClaimsPrincipal userClaims = await _userClaimsPrincipalFactory.CreateAsync(user); // Create the claimsPrincipal

            List<Claim> claims = userClaims.Claims.ToList()
                .Where(claim => context.RequestedClaimTypes.Contains(claim.Type)).ToList(); //assign only the requested claims

            // on top of the requested claims, we can also add other claims

            claims.Add(new Claim(JwtClaimTypes.FamilyName, user.LastName));
            claims.Add(new Claim(JwtClaimTypes.GivenName, user.FirstName));

            if (_userManager.SupportsUserRole)
            {
                IList<string> roles = await _userManager.GetRolesAsync(user); //get all roles assign to user

                foreach (var roleName in roles)
                {
                    claims.Add(new Claim(JwtClaimTypes.Role, roleName));

                    //we can also add the claims assigned to the role of the user
                    if (_roleManager.SupportsRoleClaims)
                    {
                        IdentityRole role = await _roleManager.FindByNameAsync(roleName);
                        if (role != null)
                        {
                            claims.AddRange(await _roleManager.GetClaimsAsync(role));
                        }
                    }
                }
            }

            //Add claims to original context object
            context.IssuedClaims = claims;

        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            string sub = context.Subject.GetSubjectId();
            ApplicationUser user = await _userManager.FindByIdAsync(sub);
            context.IsActive = user != null;
        }
    }
}
