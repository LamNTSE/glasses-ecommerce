using System;

namespace OpticalStore.DAL.Entities
{
    public class ProductVariant
    {
        public string Id { get; set; } = null!;
        public string ProductId { get; set; } = null!;

        public string? ColorName { get; set; }
        public string? SizeLabel { get; set; }
        public decimal? BridgeWidthMm { get; set; }
        public decimal? LensWidthMm { get; set; }
        public decimal? TempleLengthMm { get; set; }
        public string? FrameFinish { get; set; }

        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = null!;
        public bool IsDeleted { get; set; }
        public string OrderItemType { get; set; } = null!;

        public Product? Product { get; set; }
    }
}
