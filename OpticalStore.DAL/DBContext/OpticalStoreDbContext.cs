using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.Entities;

namespace OpticalStore.DAL.DBContext;

public partial class OpticalStoreDbContext : DbContext
{
    public OpticalStoreDbContext()
    {
    }

    public OpticalStoreDbContext(DbContextOptions<OpticalStoreDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Combo> Combos { get; set; }

    public virtual DbSet<Combo1> Combo1s { get; set; }

    public virtual DbSet<ComboItem> ComboItems { get; set; }

    public virtual DbSet<ComboItem1> ComboItem1s { get; set; }

    public virtual DbSet<ComboItem1c> ComboItem1cs { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<FeedbackImage> FeedbackImages { get; set; }

    public virtual DbSet<InvalidatedToken> InvalidatedTokens { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Len> Lens { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Policy> Policies { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<RefundRequest> RefundRequests { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Combo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("combo_pkey");

            entity.ToTable("combo");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(255)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(12, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.EndTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.IsManuallyDisabled).HasColumnName("is_manually_disabled");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.StartTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Combo1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("combo1_pkey");

            entity.ToTable("combo1");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(255)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(12, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.EndTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.IsManuallyDisabled).HasColumnName("is_manually_disabled");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.StartTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<ComboItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("combo_item_pkey");

            entity.ToTable("combo_item");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ComboId)
                .HasMaxLength(255)
                .HasColumnName("combo_id");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");
            entity.Property(e => e.ProductVariantId)
                .HasMaxLength(255)
                .HasColumnName("product_variant_id");
            entity.Property(e => e.RequiredQuantity).HasColumnName("required_quantity");

            entity.HasOne(d => d.Combo).WithMany(p => p.ComboItems)
                .HasForeignKey(d => d.ComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk8163lac8tty6571u6ensdpwr8");

            entity.HasOne(d => d.Product).WithMany(p => p.ComboItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("fk4ge3twm5tdeijqahqqxf4h9ob");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ComboItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("fk655bsj40ryv3fgl2f4t2wbiov");
        });

        modelBuilder.Entity<ComboItem1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("combo_item1_pkey");

            entity.ToTable("combo_item1");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ComboId)
                .HasMaxLength(255)
                .HasColumnName("combo_id");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");
            entity.Property(e => e.ProductVariantId)
                .HasMaxLength(255)
                .HasColumnName("product_variant_id");
            entity.Property(e => e.RequiredQuantity).HasColumnName("required_quantity");

            entity.HasOne(d => d.Combo).WithMany(p => p.ComboItem1s)
                .HasForeignKey(d => d.ComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkkifmj5iswnje5kluwfmi7khsj");

            entity.HasOne(d => d.Product).WithMany(p => p.ComboItem1s)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("fk1y2klof2wh53km1rse1blwb37");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ComboItem1s)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("fkq3a7n4qljuux4raf7w6lxmsix");
        });

        modelBuilder.Entity<ComboItem1c>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("combo_item1c_pkey");

            entity.ToTable("combo_item1c");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ComboId)
                .HasMaxLength(255)
                .HasColumnName("combo_id");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");
            entity.Property(e => e.ProductVariantId)
                .HasMaxLength(255)
                .HasColumnName("product_variant_id");
            entity.Property(e => e.RequiredQuantity).HasColumnName("required_quantity");

            entity.HasOne(d => d.Combo).WithMany(p => p.ComboItem1cs)
                .HasForeignKey(d => d.ComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk7xvm21i8vkdbnr5l3wsxten1l");

            entity.HasOne(d => d.Product).WithMany(p => p.ComboItem1cs)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("fk4c7kttks7m094rabf2xstaudp");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ComboItem1cs)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("fkaqa0gi1xafhqcvub3extujcsh");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("feedback_pkey");

            entity.ToTable("feedback");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId)
                .HasMaxLength(255)
                .HasColumnName("customer_id");
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .HasColumnName("order_id");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkihnjta1253kjyigmt2vnqbg6b");

            entity.HasOne(d => d.Order).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk66tdec0kx8px7cc7xbw3ffx8h");

            entity.HasOne(d => d.Product).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fklsfunb44jdljfmbx4un8s4waa");
        });

        modelBuilder.Entity<FeedbackImage>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("feedback_images");

            entity.Property(e => e.FeedbackId)
                .HasMaxLength(255)
                .HasColumnName("feedback_id");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("image_url");

            entity.HasOne(d => d.Feedback).WithMany()
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkivfok08kit556slsfdvlrftv1");
        });

        modelBuilder.Entity<InvalidatedToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invalidated_token_pkey");

            entity.ToTable("invalidated_token");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ExpiryTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("expiry_time");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_pkey");

            entity.ToTable("inventory");

            entity.HasIndex(e => e.ProductVariantId, "uksmhosiiwyweyooj1okkh3tsqs").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ProductVariantId)
                .HasMaxLength(255)
                .HasColumnName("product_variant_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ReservedQuantity).HasColumnName("reserved_quantity");

            entity.HasOne(d => d.ProductVariant).WithOne(p => p.Inventory)
                .HasForeignKey<Inventory>(d => d.ProductVariantId)
                .HasConstraintName("fk25ot3hf10h3y3llu3ebjru4md");
        });

        modelBuilder.Entity<Len>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lens_pkey");

            entity.ToTable("lens");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Material)
                .HasMaxLength(255)
                .HasColumnName("material");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasColumnName("price");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.ReadAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("read_at");
            entity.Property(e => e.RecipientId)
                .HasMaxLength(255)
                .HasColumnName("recipient_id");
            entity.Property(e => e.SenderId)
                .HasMaxLength(255)
                .HasColumnName("sender_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.Recipient).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkqqnsjxlwleyjbxlmm213jaj3f");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.AccountHolderName)
                .HasMaxLength(255)
                .HasColumnName("account_holder_name");
            entity.Property(e => e.BankAccountNumber)
                .HasMaxLength(255)
                .HasColumnName("bank_account_number");
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .HasColumnName("bank_name");
            entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason");
            entity.Property(e => e.CancelledAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledBy)
                .HasMaxLength(255)
                .HasColumnName("cancelled_by");
            entity.Property(e => e.ComboDiscountAmount)
                .HasPrecision(12, 2)
                .HasColumnName("combo_discount_amount");
            entity.Property(e => e.ComboId)
                .HasMaxLength(255)
                .HasColumnName("combo_id");
            entity.Property(e => e.ComboSnapshot).HasColumnName("combo_snapshot");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId)
                .HasMaxLength(255)
                .HasColumnName("customer_id");
            entity.Property(e => e.DeliveredAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("delivered_at");
            entity.Property(e => e.DeliveryAddress)
                .HasMaxLength(255)
                .HasColumnName("delivery_address");
            entity.Property(e => e.DepositAmount)
                .HasPrecision(38, 2)
                .HasColumnName("deposit_amount");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(255)
                .HasColumnName("payment_method");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(255)
                .HasColumnName("phone_number");
            entity.Property(e => e.PreOrderStatus)
                .HasMaxLength(255)
                .HasColumnName("pre_order_status");
            entity.Property(e => e.RecipientName)
                .HasMaxLength(255)
                .HasColumnName("recipient_name");
            entity.Property(e => e.RemainingAmount)
                .HasPrecision(38, 2)
                .HasColumnName("remaining_amount");
            entity.Property(e => e.ShippedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("shipped_at");
            entity.Property(e => e.ShipperId)
                .HasMaxLength(255)
                .HasColumnName("shipper_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(38, 2)
                .HasColumnName("total_amount");

            entity.HasOne(d => d.Combo).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ComboId)
                .HasConstraintName("fkpvv3pvyfjgqw0u029158kb6qr");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("fksjfs85qf6vmcurlx43cnc16gy");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_item_pkey");

            entity.ToTable("order_item");

            entity.HasIndex(e => e.PrescriptionId, "ukhhb6llg91vkplnpr0ahraitfl").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.DepositPrice)
                .HasPrecision(38, 2)
                .HasColumnName("deposit_price");
            entity.Property(e => e.InventoryId)
                .HasMaxLength(255)
                .HasColumnName("inventory_id");
            entity.Property(e => e.LensId)
                .HasMaxLength(255)
                .HasColumnName("lens_id");
            entity.Property(e => e.LensName)
                .HasMaxLength(255)
                .HasColumnName("lens_name");
            entity.Property(e => e.LensPrice)
                .HasPrecision(38, 2)
                .HasColumnName("lens_price");
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .HasColumnName("order_id");
            entity.Property(e => e.OrderItemType)
                .HasMaxLength(255)
                .HasColumnName("order_item_type");
            entity.Property(e => e.PrescriptionId)
                .HasMaxLength(255)
                .HasColumnName("prescription_id");
            entity.Property(e => e.ProductVariantId)
                .HasMaxLength(255)
                .HasColumnName("product_variant_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RemainingPrice)
                .HasPrecision(38, 2)
                .HasColumnName("remaining_price");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.TotalPrice)
                .HasPrecision(38, 2)
                .HasColumnName("total_price");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(38, 2)
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Inventory).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.InventoryId)
                .HasConstraintName("fkq5fateg3fprnc9muagexcjb");

            entity.HasOne(d => d.Lens).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.LensId)
                .HasConstraintName("fkcjo4sl1dyk51mqhbsko11kp9l");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fkt4dc2r9nbvbujrljv3e23iibt");

            entity.HasOne(d => d.Prescription).WithOne(p => p.OrderItem)
                .HasForeignKey<OrderItem>(d => d.PrescriptionId)
                .HasConstraintName("fkkb8r1rc4qycvx9t323vk230f9");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("fkasbjwtdare2wb3anogb1oai26");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_status_history_pkey");

            entity.ToTable("order_status_history");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ChangedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("changed_at");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(255)
                .HasColumnName("new_status");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(255)
                .HasColumnName("old_status");
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .HasColumnName("order_id");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_pkey");

            entity.ToTable("payment");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(38, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .HasColumnName("order_id");
            entity.Property(e => e.PaymentDate)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("payment_date");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(255)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentPurpose)
                .HasMaxLength(255)
                .HasColumnName("payment_purpose");
            entity.Property(e => e.Percentage)
                .HasPrecision(38, 2)
                .HasColumnName("percentage");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fklouu98csyullos9k25tbpk4va");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
        });

        modelBuilder.Entity<Policy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("policy_pkey");

            entity.ToTable("policy");

            entity.HasIndex(e => e.Code, "ukh1e4bssk1qe3fbiqlll0wig5i").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.ManagerUserId)
                .HasMaxLength(255)
                .HasColumnName("manager_user_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.ManagerUser).WithMany(p => p.Policies)
                .HasForeignKey(d => d.ManagerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkit4ov1jhukpsfoxnp4543wcio");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prescriptions_pkey");

            entity.ToTable("prescriptions");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("image_url");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.OdAdd).HasColumnName("od_add");
            entity.Property(e => e.OdAxis).HasColumnName("od_axis");
            entity.Property(e => e.OdCylinder).HasColumnName("od_cylinder");
            entity.Property(e => e.OdPd).HasColumnName("od_pd");
            entity.Property(e => e.OdSphere).HasColumnName("od_sphere");
            entity.Property(e => e.OsAdd).HasColumnName("os_add");
            entity.Property(e => e.OsAxis).HasColumnName("os_axis");
            entity.Property(e => e.OsCylinder).HasColumnName("os_cylinder");
            entity.Property(e => e.OsPd).HasColumnName("os_pd");
            entity.Property(e => e.OsSphere).HasColumnName("os_sphere");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_pkey");

            entity.ToTable("product");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Brand)
                .HasMaxLength(255)
                .HasColumnName("brand");
            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .HasColumnName("category");
            entity.Property(e => e.FrameMaterial)
                .HasMaxLength(255)
                .HasColumnName("frame_material");
            entity.Property(e => e.FrameType)
                .HasMaxLength(255)
                .HasColumnName("frame_type");
            entity.Property(e => e.Gender)
                .HasMaxLength(255)
                .HasColumnName("gender");
            entity.Property(e => e.HingeType)
                .HasMaxLength(255)
                .HasColumnName("hinge_type");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.ModelUrl)
                .HasMaxLength(255)
                .HasColumnName("model_url");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.NosePadType)
                .HasMaxLength(255)
                .HasColumnName("nose_pad_type");
            entity.Property(e => e.Shape)
                .HasMaxLength(255)
                .HasColumnName("shape");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.WeightGram)
                .HasPrecision(6, 2)
                .HasColumnName("weight_gram");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_image_pkey");

            entity.ToTable("product_image");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("image_url");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("fk6oo0cvcdtb6qmwsga468uuukk");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_variant_pkey");

            entity.ToTable("product_variant");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.BridgeWidthMm).HasColumnName("bridge_width_mm");
            entity.Property(e => e.ColorName)
                .HasMaxLength(255)
                .HasColumnName("color_name");
            entity.Property(e => e.FrameFinish)
                .HasMaxLength(255)
                .HasColumnName("frame_finish");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.LensWidthMm).HasColumnName("lens_width_mm");
            entity.Property(e => e.OrderItemType)
                .HasMaxLength(255)
                .HasColumnName("order_item_type");
            entity.Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasColumnName("price");
            entity.Property(e => e.ProductId)
                .HasMaxLength(255)
                .HasColumnName("product_id");
            // quantity column removed from product_variant; use Inventory table instead
            entity.Property(e => e.SizeLabel)
                .HasMaxLength(255)
                .HasColumnName("size_label");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.TempleLengthMm).HasColumnName("temple_length_mm");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fkgrbbs9t374m9gg43l6tq1xwdj");
        });

        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refund_requests_pkey");

            entity.ToTable("refund_requests");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.AccountHolderName)
                .HasMaxLength(255)
                .HasColumnName("account_holder_name");
            entity.Property(e => e.BankAccountNumber)
                .HasMaxLength(255)
                .HasColumnName("bank_account_number");
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .HasColumnName("bank_name");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId)
                .HasMaxLength(255)
                .HasColumnName("customer_id");
            entity.Property(e => e.DeductionAmount)
                .HasPrecision(38, 2)
                .HasColumnName("deduction_amount");
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .HasColumnName("order_id");
            entity.Property(e => e.OrderTotalAmount)
                .HasPrecision(38, 2)
                .HasColumnName("order_total_amount");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(255)
                .HasColumnName("payment_id");
            entity.Property(e => e.ProcessedBy)
                .HasMaxLength(255)
                .HasColumnName("processed_by");
            entity.Property(e => e.RefundAmount)
                .HasPrecision(38, 2)
                .HasColumnName("refund_amount");
            entity.Property(e => e.RefundPercentage)
                .HasPrecision(38, 2)
                .HasColumnName("refund_percentage");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.VariantId)
                .HasMaxLength(255)
                .HasColumnName("variant_id");

            entity.HasOne(d => d.Order).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fk452xm7hwgngbanwkdgs3601b1");

            entity.HasOne(d => d.Payment).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("fkrv4hfheuu2dqu3ysiykkj302g");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");

            entity.HasMany(d => d.PermissionsNames).WithMany(p => p.RoleNames)
                .UsingEntity<Dictionary<string, object>>(
                    "RolesPermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionsName")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk9u1xpvjxbdnkca024o6fyr7uu"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleName")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk6nw4jrj1tuu04j9rk7xwfssd6"),
                    j =>
                    {
                        j.HasKey("RoleName", "PermissionsName").HasName("roles_permissions_pkey");
                        j.ToTable("roles_permissions");
                        j.IndexerProperty<string>("RoleName")
                            .HasMaxLength(255)
                            .HasColumnName("role_name");
                        j.IndexerProperty<string>("PermissionsName")
                            .HasMaxLength(255)
                            .HasColumnName("permissions_name");
                    });
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transaction_pkey");

            entity.ToTable("transaction");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(38, 2)
                .HasColumnName("amount");
            entity.Property(e => e.DateTime)
                .HasColumnType("timestamp(6) without time zone")
                .HasColumnName("date_time");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.GatewayReference)
                .HasMaxLength(255)
                .HasColumnName("gateway_reference");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(255)
                .HasColumnName("payment_id");
            entity.Property(e => e.Type)
                .HasMaxLength(255)
                .HasColumnName("type");

            entity.HasOne(d => d.Payment).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("fkq9m7rb5uydysanp8smxcovxlh");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(255)
                .HasColumnName("first_name");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("image_url");
            entity.Property(e => e.LastName)
                .HasMaxLength(255)
                .HasColumnName("last_name");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .HasColumnName("phone");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");

            entity.HasMany(d => d.RoleNames).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UsersRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleName")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fkfddtbwrqg5sal9y57yyol7579"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk2o0jvgh89lemvvo17cbqvdxaa"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleName").HasName("users_roles_pkey");
                        j.ToTable("users_roles");
                        j.IndexerProperty<string>("UserId")
                            .HasMaxLength(255)
                            .HasColumnName("user_id");
                        j.IndexerProperty<string>("RoleName")
                            .HasMaxLength(255)
                            .HasColumnName("role_name");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
