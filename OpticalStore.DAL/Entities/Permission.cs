using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Permission
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Role> RoleNames { get; set; } = new List<Role>();
}
