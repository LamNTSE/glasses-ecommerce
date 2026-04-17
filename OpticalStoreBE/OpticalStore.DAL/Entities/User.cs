using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class User
{
    public string Id { get; set; } = null!;

    public DateOnly? Dob { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? ImageUrl { get; set; }

    public string? LastName { get; set; }

    public string? Password { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public string? Username { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Policy> Policies { get; set; } = new List<Policy>();

    public virtual ICollection<Role> RoleNames { get; set; } = new List<Role>();
}
