using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IServices.IResidentials;
using IServices.IUser;
using Models.Dominio;
using Models.WebApi.Users;

namespace Service.UserServicess;

public class UserService (IResidentialService residentialService, IUserEntityService userEntityService) :  IUserService
{
    private readonly IResidentialService _residentialService = residentialService ;
    private readonly IUserEntityService _userEntityService = userEntityService ;
    public CreateUserDtoFromBack createUser(CreateUserDtoFromBack dto)
    {
        ResidentialDto residencialBuscado = _residentialService.GetById(dto._residentialId);
        string ipDestino = residencialBuscado._ipActual;

        // Para ISAPI (Digest), si hay credenciales en variables de entorno,
        // el handler las usa durante el challenge/response del reloj.
        string? isapiUser = Environment.GetEnvironmentVariable("ISAPI_USER");
        string? isapiPassword = Environment.GetEnvironmentVariable("ISAPI_PASSWORD");
        HttpClientHandler handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(isapiUser) && !string.IsNullOrWhiteSpace(isapiPassword))
        {
            handler.Credentials = new NetworkCredential(isapiUser, isapiPassword);
        }

        using HttpClient httpClient = new HttpClient(handler);

        foreach (var reloj in residencialBuscado._relojes)
        {
            int puerto = reloj._puerto;
            CreateUserDtoToClock pronto = _userEntityService.FromCreateUserDtoFromBackToClock(dto);

            // 1) Arma la URL destino del reloj (ruta ISAPI + querystring format=json).
            // Nota: 8443/443 normalmente se usan en HTTPS.
            string scheme = (puerto == 443 || puerto == 8443) ? "https" : "http";
            string endpoint = $"{scheme}://{ipDestino}:{puerto}/ISAPI/AccessControl/UserInfo/SetUp?format=json";

            // 2) Arma el body EXACTO esperado por ISAPI:
            // {
            //   "UserInfo": {
            //     "employeeNo": "...",
            //     "name": "...",
            //     "userType": "...",
            //     "Valid": { ... }
            //   }
            // }
            var body = new
            {
                UserInfo = new
                {
                    employeeNo = pronto._employeeNo,
                    name = pronto._name,
                    userType = pronto._userType,
                    Valid = new
                    {
                        enable = pronto._enable,
                        beginTime = pronto._beginTime,
                        endTime = pronto._endTime,
                        timeType = pronto._timeType
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(body);

            // 3) Crea la request HTTP explicitando verbo, ruta y content-type.
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // 4) Envía la request y valida status de respuesta.
            using HttpResponseMessage response = httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = response.Content.ReadAsStringAsync().Result;
                throw new InvalidOperationException(
                    $"Error creando usuario en reloj {reloj._idReloj} ({endpoint}). " +
                    $"Status={(int)response.StatusCode} Body={errorBody}");
            }
        }

        // Si todos los relojes respondieron OK, devolvemos el DTO original del back.
        return dto;
    }

    public ModifiUserDtoFromBack modifyUser(ModifiUserDtoFromBack dto)
    {
        ResidentialDto residencialBuscado = _residentialService.GetById(dto._residentialId);
        string ipDestino = residencialBuscado._ipActual;

        // Para ISAPI (Digest), si hay credenciales en variables de entorno,
        // el handler las usa durante el challenge/response del reloj.
        string? isapiUser = Environment.GetEnvironmentVariable("ISAPI_USER");
        string? isapiPassword = Environment.GetEnvironmentVariable("ISAPI_PASSWORD");
        HttpClientHandler handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(isapiUser) && !string.IsNullOrWhiteSpace(isapiPassword))
        {
            handler.Credentials = new NetworkCredential(isapiUser, isapiPassword);
        }

        using HttpClient httpClient = new HttpClient(handler);

        foreach (var reloj in residencialBuscado._relojes)
        {
            int puerto = reloj._puerto;
            ModifiUserDtoToClock pronto = _userEntityService.FromModifyUserDtoFromBackToClock(dto);

            // 1) Arma la URL destino del reloj para editar persona.
            string scheme = (puerto == 443 || puerto == 8443) ? "https" : "http";
            string endpoint = $"{scheme}://{ipDestino}:{puerto}/ISAPI/AccessControl/UserInfo/Modify?format=json";

            // 2) Arma el body esperado por ISAPI para Modify.
            var body = new
            {
                UserInfo = new
                {
                    employeeNo = pronto._employeeNo,
                    name = pronto._name,
                    userType = pronto._userType,
                    Valid = new
                    {
                        enable = pronto._enable,
                        beginTime = pronto._beginTime,
                        endTime = pronto._endTime,
                        timeType = pronto._timeType
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(body);

            // 3) Crea la request HTTP explicitando verbo, ruta y content-type.
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // 4) Envía la request y valida status de respuesta.
            using HttpResponseMessage response = httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = response.Content.ReadAsStringAsync().Result;
                throw new InvalidOperationException(
                    $"Error modificando usuario en reloj {reloj._idReloj} ({endpoint}). " +
                    $"Status={(int)response.StatusCode} Body={errorBody}");
            }
        }

        // Si todos los relojes respondieron OK, devolvemos el DTO original del back.
        return dto;
    }

    public DeleteUserDtoFromBack deleteUser(DeleteUserDtoFromBack dto)
    {
        ResidentialDto residencialBuscado = _residentialService.GetById(dto._residentialId);
        string ipDestino = residencialBuscado._ipActual;

        // Para ISAPI (Digest), si hay credenciales en variables de entorno,
        // el handler las usa durante el challenge/response del reloj.
        string? isapiUser = Environment.GetEnvironmentVariable("ISAPI_USER");
        string? isapiPassword = Environment.GetEnvironmentVariable("ISAPI_PASSWORD");
        HttpClientHandler handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(isapiUser) && !string.IsNullOrWhiteSpace(isapiPassword))
        {
            handler.Credentials = new NetworkCredential(isapiUser, isapiPassword);
        }

        using HttpClient httpClient = new HttpClient(handler);

        foreach (var reloj in residencialBuscado._relojes)
        {
            int puerto = reloj._puerto;

            // 1) Arma la URL destino del reloj para borrar persona.
            string scheme = (puerto == 443 || puerto == 8443) ? "https" : "http";
            string endpoint = $"{scheme}://{ipDestino}:{puerto}/ISAPI/AccessControl/UserInfoDetail/Delete?format=json";

            // 2) Arma el body esperado por ISAPI para Delete por employeeNo.
            var body = new
            {
                UserInfoDetail = new
                {
                    mode = "byEmployeeNo",
                    EmployeeNoList = new[]
                    {
                        new
                        {
                            employeeNo = dto._employeeNo
                        }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(body);

            // 3) Crea la request HTTP explicitando verbo, ruta y content-type.
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // 4) Envía la request y valida status de respuesta.
            using HttpResponseMessage response = httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = response.Content.ReadAsStringAsync().Result;
                throw new InvalidOperationException(
                    $"Error borrando usuario en reloj {reloj._idReloj} ({endpoint}). " +
                    $"Status={(int)response.StatusCode} Body={errorBody}");
            }
        }

        // Si todos los relojes respondieron OK, devolvemos el DTO original del back.
        return dto;
    }
}
