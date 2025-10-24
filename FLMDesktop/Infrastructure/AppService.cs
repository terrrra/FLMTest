using FLMDesktop.Services;
using System;

namespace FLMDesktop.Infrastructure
{
    public static class InitializeServices
    {

        public static IBranchService Branches { get; private set; } = default!;
        public static IProductService Products { get; private set; } = default!;
        public static IAssignmentService Assignments { get; private set; } = default!;
        public static string ConnectionString { get; private set; } = "";

        public static void Init(string cs)
        {
            if (string.IsNullOrWhiteSpace(cs))
                throw new ArgumentException("Connection string is null or empty.", nameof(cs));
            
            ConnectionString = cs;
            Branches = new FLMDesktop.Services.BranchService(cs);
            Products = new FLMDesktop.Services.ProductService(cs);
            Assignments = new FLMDesktop.Services.AssignmentService(cs);
        }
    }
}