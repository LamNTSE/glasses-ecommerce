using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Entities.Enums;

namespace OpticalStore.DAL.DBContext
{
    public class OpticalStoreDbContext : DbContext
    {
        public OpticalStoreDbContext(DbContextOptions<OpticalStoreDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductVariant> ProductVariants { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("Users", tb =>
                {
                    tb.HasCheckConstraint("CK_Users_Status", $"\"Status\" IN ('{StatusValues.Active}','{StatusValues.Inactive}')");
                });
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(255).IsRequired();
                b.Property(x => x.Dob).HasColumnType("date");
                b.Property(x => x.Email).HasMaxLength(255).IsRequired();
                b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
                b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
                b.Property(x => x.Username).HasMaxLength(100).IsRequired();
                b.Property(x => x.Password).HasMaxLength(255).IsRequired();
                b.Property(x => x.Phone).HasColumnType("varchar(20)");
                b.Property(x => x.ImageUrl).HasMaxLength(500);
                b.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
                b.Property(x => x.RefreshToken).HasMaxLength(500);
                b.Property(x => x.RefreshTokenExpiryTime);

                b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_Users_Email");
                b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_Users_Username");
                b.HasIndex(x => x.Phone).IsUnique().HasDatabaseName("UX_Users_Phone").HasFilter("\"Phone\" IS NOT NULL");
            });

            // Products
            modelBuilder.Entity<Product>(b =>
            {
                b.ToTable("Products", tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_Products_Category",
                        $"\"Category\" IN ('{ProductCategoryValues.Frame}','{ProductCategoryValues.Lens}','{ProductCategoryValues.Accessory}')");
                    tb.HasCheckConstraint("CK_Products_Status", $"\"Status\" IN ('{StatusValues.Active}','{StatusValues.Inactive}')");
                });
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(255).IsRequired();
                b.Property(x => x.Name).HasMaxLength(255).IsRequired();
                b.Property(x => x.Brand).HasMaxLength(150);
                b.Property(x => x.Category).HasColumnType("varchar(20)").IsRequired();
                b.Property(x => x.FrameMaterial).HasMaxLength(100);
                b.Property(x => x.FrameType).HasMaxLength(100);
                b.Property(x => x.Gender).HasColumnType("varchar(20)");
                b.Property(x => x.HingeType).HasMaxLength(100);
                b.Property(x => x.NosePadType).HasMaxLength(100);
                b.Property(x => x.Shape).HasMaxLength(100);
                b.Property(x => x.WeightGram).HasColumnType("numeric(6,2)");
                b.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
                b.Property(x => x.ModelUrl).HasMaxLength(500);

                b.Property(x => x.IsDeleted).HasDefaultValue(false);

                b.HasQueryFilter(x => !x.IsDeleted);
            });

            // ProductVariants
            modelBuilder.Entity<ProductVariant>(b =>
            {
                b.ToTable("ProductVariants", tb =>
                {
                    tb.HasCheckConstraint("CK_ProductVariants_Price", "\"Price\" >= 0");
                    tb.HasCheckConstraint("CK_ProductVariants_Quantity", "\"Quantity\" >= 0");
                    tb.HasCheckConstraint("CK_ProductVariants_Status", $"\"Status\" IN ('{StatusValues.Active}','{StatusValues.Inactive}')");
                });
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(255).IsRequired();
                b.Property(x => x.ProductId).HasMaxLength(255).IsRequired();
                b.Property(x => x.ColorName).HasMaxLength(100);
                b.Property(x => x.SizeLabel).HasMaxLength(50);
                b.Property(x => x.BridgeWidthMm).HasColumnType("numeric(6,2)");
                b.Property(x => x.LensWidthMm).HasColumnType("numeric(6,2)");
                b.Property(x => x.TempleLengthMm).HasColumnType("numeric(6,2)");
                b.Property(x => x.FrameFinish).HasMaxLength(100);
                b.Property(x => x.Price).HasColumnType("numeric(12,2)").IsRequired();
                b.Property(x => x.Quantity).HasDefaultValue(0);
                b.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
                b.Property(x => x.IsDeleted).HasDefaultValue(false);
                b.Property(x => x.OrderItemType).HasMaxLength(100).IsRequired();

                b.HasOne(x => x.Product).WithMany(p => p.ProductVariants).HasForeignKey(x => x.ProductId).HasConstraintName("FK_ProductVariants_Products");

                b.HasIndex(x => x.ProductId).HasDatabaseName("IX_ProductVariants_ProductId");

                b.HasQueryFilter(x => !x.IsDeleted);
            });
        }
    }
}
