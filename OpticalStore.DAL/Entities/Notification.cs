using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Notification
{
    public string Id { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? SenderId { get; set; }

    public string Title { get; set; } = null!;

    public string RecipientId { get; set; } = null!;

    public virtual User Recipient { get; set; } = null!;
}
