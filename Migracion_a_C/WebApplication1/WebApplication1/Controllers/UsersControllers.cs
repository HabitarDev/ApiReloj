using IServices.IUser;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models.WebApi.Users;

namespace WebApplication1.Controllers;
[ApiController]
[Route("[controller]")]
public class UsersControllers (IUserService userService) :ControllerBase
{
    private readonly IUserService _userService = userService ;
    [HttpPost]
    public ActionResult<CreateUserDtoFromBack> crearUsuario(CreateUserDtoFromBack user)
    {
        return Ok(_userService.createUser(user));
    }

    [HttpPut]
    public ActionResult<ModifiUserDtoFromBack> modificarUsuario(ModifiUserDtoFromBack user)
    {
        return Ok(_userService.modifyUser(user));
    }

    [HttpDelete]
    public ActionResult<DeleteUserDtoFromBack> deletarUsuario(DeleteUserDtoFromBack user)
    {
        return Ok(_userService.deleteUser(user));
    }
}