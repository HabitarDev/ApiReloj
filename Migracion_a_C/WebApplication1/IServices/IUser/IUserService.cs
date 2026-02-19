using Models.Dominio;
using Models.WebApi.Users;

namespace IServices.IUser;

public interface IUserService
{
    public CreateUserDtoFromBack createUser(CreateUserDtoFromBack dto);
    public ModifiUserDtoFromBack modifyUser(ModifiUserDtoFromBack dto);
    public DeleteUserDtoFromBack deleteUser(DeleteUserDtoFromBack dto);
}