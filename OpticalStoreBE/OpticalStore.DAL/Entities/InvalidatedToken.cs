using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class InvalidatedToken
{
    public string Id { get; set; } = null!;

    public DateTime? ExpiryTime { get; set; }
}
