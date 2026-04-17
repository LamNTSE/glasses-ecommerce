using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Policy
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public string? Description { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string Title { get; set; } = null!;

    public string ManagerUserId { get; set; } = null!;

    public virtual User ManagerUser { get; set; } = null!;
}
