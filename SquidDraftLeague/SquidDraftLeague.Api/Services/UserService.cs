using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SquidDraftLeague.Api.Models;
using SquidDraftLeague.MySQL;
using SquidDraftLeague.MySQL.Entities;

namespace SquidDraftLeague.Api.Services
{
    public class UserService : IUserService
    {
        private readonly TokenManagement appSettings;

        public UserService(IOptions<TokenManagement> appSettings)
        {
            this.appSettings = appSettings.Value;
        }

        public async Task<ApiUser> Authenticate(string username, string password)
        {
            

            ApiUser[] users = await MySqlClient.GetApiUsers();
            ApiUser user = users.SingleOrDefault(x => x.UserName == username && x.PasswordHash == password);

            // return null if user not found
            if (user == null)
                return null;

            // authentication successful so generate jwt token
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            byte[] key = Encoding.ASCII.GetBytes(this.appSettings.Secret);
            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Id)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            user.Token = tokenHandler.WriteToken(token);

            return user;
        }

        public async Task<IEnumerable<ApiUser>> GetAll()
        {
            return await MySqlClient.GetApiUsers();
        }
    }
}
