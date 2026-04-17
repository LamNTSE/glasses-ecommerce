using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Role
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Permission> PermissionsNames { get; set; } = new List<Permission>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
