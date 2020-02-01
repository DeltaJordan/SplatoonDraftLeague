using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace SquidDraftLeague.MySQL.Entities
{
    public class ApiUser : IdentityUser
    {
        public string Role { get; set; }
        public string Token { get; set; }
    }
}
