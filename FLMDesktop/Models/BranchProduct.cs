namespace FLMDesktop.Models;

public class BranchProduct
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}
