using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SquidDraftLeague.Api.Models;
using SquidDraftLeague.Api.Services;
using SquidDraftLeague.MySQL.Entities;

namespace SquidDraftLeague.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService userService;

        public UsersController(IUserService userService)
        {
            this.userService = userService;
        }

        // TODO Very insecure as it sends the user's entire information back.
        // Most likely this is intended for a back-to-front communication setup,
        // which is not needed for this.
        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody]AuthenticateModel model)
        {
            ApiUser user = await this.userService.Authenticate(model.Username, model.PasswordHash);

            if (user == null)
                return this.BadRequest(new { message = "Username or password is incorrect" });

            // This might be enough?
            user.PasswordHash = null;

            return this.Ok($"{{\"token\": \"{user.Token}\"}}");
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            Task<IEnumerable<ApiUser>> users = this.userService.GetAll();
            return this.Ok(users);
        }
    }
}
