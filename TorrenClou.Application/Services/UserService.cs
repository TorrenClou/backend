using TorrenClou.Core.Entities;
using TorrenClou.Core.Interfaces;
using TorrenClou.Core.Specifications;

namespace TorrenClou.Application.Services
{
    public class UserService(IUnitOfWork unitOfWork) : IUserService
    {
        public async Task<bool> UserExistsAsync(string email)
        {
            var spec = new BaseSpecification<User>(u => u.Email.ToLower() == email.ToLower());
            var user = await unitOfWork.Repository<User>().GetEntityWithSpec(spec);
            return user != null;
        }
        public async Task<User> CreateUser(string email, string name)
        {
            var user = new User
            {
                Email = email,
                FullName = name
            };
            unitOfWork.Repository<User>().Add(user);
            await unitOfWork.Complete();
            return user;

        }
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var spec = new BaseSpecification<User>(u => u.Email.ToLower() == email.ToLower());
            return await unitOfWork.Repository<User>().GetEntityWithSpec(spec);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await unitOfWork.Repository<User>().GetByIdAsync(userId);
        }

        public async Task SetPasswordHashAsync(User user, string passwordHash)
        {
            user.PasswordHash = passwordHash;
            await unitOfWork.Complete();
        }
    }
}
