using FLMDesktop.Services;

namespace FLMDesktop.Infrastructure
{
    public static class AppServices
    {
        public static IBranchService Branches { get; private set; } = default!;
        public static IProductService Products { get; private set; } = default!;
        public static IAssignmentService Assignments { get; private set; } = default!;
        public static string ConnectionString { get; private set; } = "";

        public static void Init(string cs)
        {
            Branches = new FLMDesktop.Services.BranchService(cs);
            Products = new FLMDesktop.Services.ProductService(cs);
            Assignments = new FLMDesktop.Services.AssignmentService(cs);
        }
    }
}