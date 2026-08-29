using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShippingManagementApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarriersAndServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carriers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SupportsPickup = table.Column<bool>(type: "bit", nullable: false),
                    SupportsTracking = table.Column<bool>(type: "bit", nullable: false),
                    SupportsCancellation = table.Column<bool>(type: "bit", nullable: false),
                    SupportsCod = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carriers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarrierServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedMinDays = table.Column<int>(type: "int", nullable: false),
                    EstimatedMaxDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierServices", x => x.Id);
                    table.CheckConstraint("CK_CarrierServices_EstimatedMinDays", "[EstimatedMinDays] >= 0");
                    table.CheckConstraint("CK_CarrierServices_EstimatedRange", "[EstimatedMaxDays] >= [EstimatedMinDays]");
                    table.ForeignKey(
                        name: "FK_CarrierServices_Carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "Carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Carriers",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "SupportsCancellation", "SupportsCod", "SupportsPickup", "SupportsTracking", "UpdatedAtUtc" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), "DEMO", new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Demo Carrier", true, true, true, true, new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "CarrierServices",
                columns: new[] { "Id", "CarrierId", "Code", "CreatedAtUtc", "EstimatedMaxDays", "EstimatedMinDays", "IsActive", "Name", "ServiceLevel", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("44444444-4444-4444-4444-444444444444"), "STANDARD", new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5, 2, true, "Demo Standard", "Standard", new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("44444444-4444-4444-4444-444444444444"), "EXPRESS", new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 1, true, "Demo Express", "Express", new DateTimeOffset(new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carriers_Code",
                table: "Carriers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carriers_IsActive",
                table: "Carriers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CarrierServices_CarrierId",
                table: "CarrierServices",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_CarrierServices_CarrierId_Code",
                table: "CarrierServices",
                columns: new[] { "CarrierId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarrierServices_IsActive",
                table: "CarrierServices",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarrierServices");

            migrationBuilder.DropTable(
                name: "Carriers");
        }
    }
}
