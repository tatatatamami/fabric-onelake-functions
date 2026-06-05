namespace function_onelake.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}

public class EmployeeResponse
{
    public int Total { get; set; }
    public string Department { get; set; } = string.Empty;
    public decimal AverageSalary { get; set; }
    public List<Employee> Items { get; set; } = new();
}