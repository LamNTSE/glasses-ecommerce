using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class FeedbackImage
{
    public string FeedbackId { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}
