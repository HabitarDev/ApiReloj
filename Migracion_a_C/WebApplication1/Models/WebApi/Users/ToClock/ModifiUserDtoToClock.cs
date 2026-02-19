using System.ComponentModel.DataAnnotations;

namespace Models.WebApi.Users;

public class ModifiUserDtoToClock
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
}