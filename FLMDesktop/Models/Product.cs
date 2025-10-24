using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FLMDesktop.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool WeightedItem { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal SuggestedSellingPrice { get; set; }

    public ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();
}
