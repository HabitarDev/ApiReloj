using Models.WebApi.Users;

namespace IServices.IUser;

public interface IUserEntityService
{
    public CreateUserDtoToClock FromCreateUserDtoFromBackToClock(CreateUserDtoFromBack userDto);
    public ModifiUserDtoToClock FromModifyUserDtoFromBackToClock(ModifiUserDtoFromBack userDto);

}