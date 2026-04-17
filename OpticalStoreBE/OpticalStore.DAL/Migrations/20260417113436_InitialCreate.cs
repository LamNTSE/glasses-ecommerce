using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticalStore.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Brand = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Category = table.Column<string>(type: "varchar(20)", nullable: false),
                    FrameMaterial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FrameType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "varchar(20)", nullable: true),
                    HingeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NosePadType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Shape = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WeightGram = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ModelUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Category", "\"Category\" IN ('FRAME','LENS','ACCESSORY')");
                    table.CheckConstraint("CK_Products_Status", "\"Status\" IN ('ACTIVE','INACTIVE')");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Dob = table.Column<DateTime>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefreshTokenExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Status", "\"Status\" IN ('ACTIVE','INACTIVE')");
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProductId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ColorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SizeLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BridgeWidthMm = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    LensWidthMm = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    TempleLengthMm = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    FrameFinish = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OrderItemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.CheckConstraint("CK_ProductVariants_Price", "\"Price\" >= 0");
                    table.CheckConstraint("CK_ProductVariants_Quantity", "\"Quantity\" >= 0");
                    table.CheckConstraint("CK_ProductVariants_Status", "\"Status\" IN ('ACTIVE','INACTIVE')");
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true,
                filter: "\"Phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
