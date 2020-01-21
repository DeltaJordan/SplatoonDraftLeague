using System.ComponentModel.DataAnnotations;

namespace SquidDraftLeague.Api.Models
{
    public class AuthenticateModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }
    }
}