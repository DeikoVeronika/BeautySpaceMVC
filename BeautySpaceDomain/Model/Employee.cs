using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautySpaceDomain.Model;

public partial class Employee : Entity
{
    private string _firstName;
    private string _lastName;

    [Required(ErrorMessage = "Введіть ім'я працівника")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ім'я повинно містити у собі принаймні два символи")]
    [Display(Name = "Ім'я")]

    public string FirstName
    {
        get => _firstName;
        set => _firstName = value.Substring(0, 1).ToUpperInvariant() + value.Substring(1);
    }
    [Display(Name = "Прізвище (необов'язково)")]
    public string? LastName
    {
        get => _lastName;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                _lastName = value.Substring(0, 1).ToUpperInvariant() + value.Substring(1);
            }
            else
            {
                _lastName = value;
            }
        }
    }
    public int PositionId { get; set; }

    public byte[]? EmployeePortrait { get; set; }

    [Required(ErrorMessage = "Введіть мобільний номер працівника")]
    [RegularExpression(@"^\+380 \(\d{2}\) \d{3}-\d{2}-\d{2}$", ErrorMessage = "Введіть коректний номер телефону")]
    [Display(Name = "Мобільний номер")]
    public string PhoneNumber { get; set; } = null!;

    public virtual ICollection<EmployeeService> EmployeeServices { get; set; } = new List<EmployeeService>();

    public virtual Position Position { get; set; } = null!;

    public string FullName
    {
        get
        {
            return $" {LastName} {FirstName}";
        }
    }

}
