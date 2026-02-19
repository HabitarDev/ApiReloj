namespace Models.WebApi.Users;

public class CreateUserDtoFromBack
{
    public string _employeeNo { get; set; } = null!;
    public string _name { get; set; } = null!;
    public string _userType { get; set; } = "normal";
    public string _beginTime { get; set; } = "2000-01-01T00:00:00";
    public string _endTime { get; set; } = "2037-12-31T23:59:59";
    public bool _enable { get; set; } = true;
    public string _timeType { get; set; } = "local";
    public int _residentialId { get; set; }
}
