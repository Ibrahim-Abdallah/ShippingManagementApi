using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingManagementApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShippingQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    OriginCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginStateOrProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OriginAddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinationCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DestinationCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DestinationStateOrProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DestinationAddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingQuotes", x => x.Id);
                    table.CheckConstraint("CK_ShippingQuotes_Expiration", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ShippingQuotes_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarrierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CarrierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CarrierServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EstimatedMinDays = table.Column<int>(type: "int", nullable: false),
                    EstimatedMaxDays = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteOptions", x => x.Id);
                    table.CheckConstraint("CK_QuoteOptions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_QuoteOptions_EstimatedRange", "[EstimatedMinDays] >= 0 AND [EstimatedMaxDays] >= [EstimatedMinDays]");
                    table.ForeignKey(
                        name: "FK_QuoteOptions_ShippingQuotes_ShippingQuoteId",
                        column: x => x.ShippingQuoteId,
                        principalTable: "ShippingQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShippingQuotePackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    WeightUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Length = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    DimensionUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingQuotePackages", x => x.Id);
                    table.CheckConstraint("CK_ShippingQuotePackages_Weight", "[Weight] > 0");
                    table.ForeignKey(
                        name: "FK_ShippingQuotePackages_ShippingQuotes_ShippingQuoteId",
                        column: x => x.ShippingQuoteId,
                        principalTable: "ShippingQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOptions_ShippingQuoteId",
                table: "QuoteOptions",
                column: "ShippingQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOptions_ShippingQuoteId_CarrierServiceId",
                table: "QuoteOptions",
                columns: new[] { "ShippingQuoteId", "CarrierServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShippingQuotePackages_ShippingQuoteId",
                table: "ShippingQuotePackages",
                column: "ShippingQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingQuotes_ExpiresAtUtc",
                table: "ShippingQuotes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingQuotes_MerchantId_CreatedAtUtc",
                table: "ShippingQuotes",
                columns: new[] { "MerchantId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteOptions");

            migrationBuilder.DropTable(
                name: "ShippingQuotePackages");

            migrationBuilder.DropTable(
                name: "ShippingQuotes");
        }
    }
}
