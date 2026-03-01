using System.ComponentModel.DataAnnotations;

namespace Models.WebApi.Users;

public class ModifiUserDtoFromBack
{
    [Required]
    public string _employeeNo { get; set; } = null!;

    [Required]
    public string _name { get; set; } = null!;

    [Required]
    public string _userType { get; set; } = null!;

    [Required]
    public string _beginTime { get; set; } = null!;

    [Required]
    public string _endTime { get; set; } = null!;

    [Required]
    public bool? _enable { get; set; }

    [Required]
    public string _timeType { get; set; } = null!;

    public int _residentialId { get; set; }

    public ModifiUserDtoFromBack()
    {
    }

    public ModifiUserDtoFromBack(string employeeNo, string name, string userType, string beginTime, string endTime,
        bool? enable, string timeType, int residentialId)
    {
        _employeeNo = employeeNo;
        _name = name;
        _userType = userType;
        _beginTime = beginTime;
        _endTime = endTime;
        _enable = enable;
        _timeType = timeType;
        _residentialId = residentialId;
    }
}
