using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Product
{
    public string Id { get; set; } = null!;

    public string? Brand { get; set; }

    public string? Category { get; set; }

    public string? FrameMaterial { get; set; }

    public string? FrameType { get; set; }

    public string? Gender { get; set; }

    public string? HingeType { get; set; }

    public string Name { get; set; } = null!;

    public string? NosePadType { get; set; }

    public string? Shape { get; set; }

    public string Status { get; set; } = null!;

    public decimal? WeightGram { get; set; }

    public bool? IsDeleted { get; set; }

    public string? ModelUrl { get; set; }

    public virtual ICollection<ComboItem1c> ComboItem1cs { get; set; } = new List<ComboItem1c>();

    public virtual ICollection<ComboItem1> ComboItem1s { get; set; } = new List<ComboItem1>();

    public virtual ICollection<ComboItem> ComboItems { get; set; } = new List<ComboItem>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
