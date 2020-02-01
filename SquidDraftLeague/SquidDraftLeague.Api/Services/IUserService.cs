using System.Collections.Generic;
using System.Threading.Tasks;
using SquidDraftLeague.MySQL.Entities;

namespace SquidDraftLeague.Api.Services
{
    public interface IUserService
    {
        Task<ApiUser> Authenticate(string username, string password);
        Task<IEnumerable<ApiUser>> GetAll();
    }
}