using IServices.IUser;
using Models.WebApi.Users;

namespace Service.UserServicess;

public class UserEntityService : IUserEntityService
{
    public CreateUserDtoToClock FromCreateUserDtoFromBackToClock(CreateUserDtoFromBack userDto)
    {
        CreateUserDtoToClock aReetornar = new CreateUserDtoToClock();
        aReetornar._beginTime = userDto._beginTime;
        aReetornar._endTime = userDto._endTime;
        aReetornar._timeType = userDto._timeType;
        aReetornar._employeeNo = userDto._employeeNo;
        aReetornar._enable = userDto._enable;
        aReetornar._userType = userDto._userType;
        aReetornar._name = userDto._name;
        return aReetornar;
    }

    public ModifiUserDtoToClock FromModifyUserDtoFromBackToClock(ModifiUserDtoFromBack userDto)
    {
        ModifiUserDtoToClock aReetornar = new ModifiUserDtoToClock();
        aReetornar._beginTime = userDto._beginTime;
        aReetornar._endTime = userDto._endTime;
        aReetornar._timeType = userDto._timeType;
        aReetornar._employeeNo = userDto._employeeNo;
        aReetornar._enable = userDto._enable;
        aReetornar._userType = userDto._userType;
        aReetornar._name = userDto._name;
        return aReetornar;
    }
}