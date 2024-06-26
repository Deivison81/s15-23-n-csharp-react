﻿using Cacke.Identity.Constant;
using Cacke.Identity.Models;
using Cacke.Identity.Service.Contract;
using CakeBack.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace Cacke.Identity.Service.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly JwtSettings _jwtSettings;

        public AuthService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IOptions<JwtSettings> jwtSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<AuthResponse> Login(AuthRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            
            if (user == null)
            {
                throw new Exception($"El usuario con email{request.Email} no existe");

            }

            var result = await  _signInManager.PasswordSignInAsync(user.UserName, request.Password, false, lockoutOnFailure: false);

            if (!result.Succeeded) 
            {
                throw new Exception($"Las credenciales son Incorrectas");
            }

            var token = await GenerateToken(user);

            var authResponse = new AuthResponse
            {
                Id = user.Id,
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Email = user.Email,
                UserName = user.UserName,

            };

            return authResponse;
        }

        public async Task<RegistrationResponse> Register(RegistrationRequest request)
        {
            var existingUser = await _userManager.FindByNameAsync(request.Username);
            
            if (existingUser != null) 
            {
                throw new Exception($"El {request.Username} ya fue tomado por otro usuario");
            }

            var existingEmail = await _userManager.FindByEmailAsync(request.Email);

            if (existingEmail != null)
            {
                throw new Exception($"El {request.Email} ya fue tomado por otro usuario");
            }

            var user = new ApplicationUser
            {
                Email = request.Email,

                Nombre = request.Name,

                Apellidos = request.LastName,

                UserName = request.Username,

                EmailConfirmed = true

            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded) 
            {
                await _userManager.AddToRoleAsync(user, "Operator");

                var token = await GenerateToken(user);

                return new RegistrationResponse
                {
                    Email = user.Email,
                    
                    Token= new JwtSecurityTokenHandler().WriteToken(token),

                    UserId = user.Id,

                    UserName= user.UserName,
                };

            }
            throw new Exception($"{result.Errors}");
        }


        public async Task<JwtSecurityToken> GenerateToken(ApplicationUser user) 
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            
            var roles = await _userManager.GetRolesAsync(user); 

            var roleClaims = new List<Claim>();

            foreach ( var role in roles)
            {
                roleClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(CustomClaimTypes.Uid, user.Id)
            }.Union(userClaims).Union(roleClaims);

            var symetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var signingCredentials = new SigningCredentials(symetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken =  new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinute),
                    signingCredentials: signingCredentials);


            return jwtSecurityToken;

        }
    }
}
