using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FLMDesktop.Models;

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TelephoneNumber { get; set; }
    public DateTime? OpenDate { get; set; }

    // ADD THIS For EFCore to be able to assign the correct FK and Composites
    public ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();

    public Branch() { }

    public Branch(int id, string name, string telephoneNumber, DateTime openDate)
    {
        Id = id; Name = name; TelephoneNumber = telephoneNumber; OpenDate = openDate;

    }
}
