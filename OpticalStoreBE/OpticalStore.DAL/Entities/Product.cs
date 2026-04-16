using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities
{
    public class Product
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Brand { get; set; }
        public string Category { get; set; } = null!;
        public string? FrameMaterial { get; set; }
        public string? FrameType { get; set; }
        public string? Gender { get; set; }
        public string? HingeType { get; set; }
        public string? NosePadType { get; set; }
        public string? Shape { get; set; }
        public decimal? WeightGram { get; set; }
        public string Status { get; set; } = null!;

        public bool IsDeleted { get; set; }
        public string? ModelUrl { get; set; }

        public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
    }
}
