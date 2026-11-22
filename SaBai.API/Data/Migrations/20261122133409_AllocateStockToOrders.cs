using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaBai.API.Migrations
{
    /// <inheritdoc />
    public partial class AllocateStockToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_MedicationBrands_MedicationBrandId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_StaffUsers_ApprovedById",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.RenameColumn(
                name: "MedicationBrandId",
                table: "Orders",
                newName: "MedicationActiveIngredientId");

            migrationBuilder.RenameColumn(
                name: "ApprovedById",
                table: "Orders",
                newName: "DispensedById");

            migrationBuilder.RenameColumn(
                name: "ApprovedAt",
                table: "Orders",
                newName: "DispensedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_MedicationBrandId",
                table: "Orders",
                newName: "IX_Orders_MedicationActiveIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_ApprovedById",
                table: "Orders",
                newName: "IX_Orders_DispensedById");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "StockChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentStockId",
                table: "MedicationStocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "MedicationStocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MedicationStocks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "OrderAllocations",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAllocations", x => new { x.OrderId, x.StockId });
                    table.CheckConstraint("ck_order_allocations_quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderAllocations_MedicationStocks_StockId",
                        column: x => x.StockId,
                        principalTable: "MedicationStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderAllocations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_OrderId",
                table: "StockChanges",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationStocks_ParentStockId",
                table: "MedicationStocks",
                column: "ParentStockId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_medication_stocks_quantity_non_negative",
                table: "MedicationStocks",
                sql: "\"Quantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_medication_stocks_reserved_within_quantity",
                table: "MedicationStocks",
                sql: "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAllocations_StockId",
                table: "OrderAllocations",
                column: "StockId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicationStocks_MedicationStocks_ParentStockId",
                table: "MedicationStocks",
                column: "ParentStockId",
                principalTable: "MedicationStocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MedicationActiveIngredients_MedicationActiveIngredie~",
                table: "Orders",
                column: "MedicationActiveIngredientId",
                principalTable: "MedicationActiveIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_StaffUsers_DispensedById",
                table: "Orders",
                column: "DispensedById",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockChanges_Orders_OrderId",
                table: "StockChanges",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicationStocks_MedicationStocks_ParentStockId",
                table: "MedicationStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_MedicationActiveIngredients_MedicationActiveIngredie~",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_StaffUsers_DispensedById",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_StockChanges_Orders_OrderId",
                table: "StockChanges");

            migrationBuilder.DropTable(
                name: "OrderAllocations");

            migrationBuilder.DropIndex(
                name: "IX_StockChanges_OrderId",
                table: "StockChanges");

            migrationBuilder.DropIndex(
                name: "IX_MedicationStocks_ParentStockId",
                table: "MedicationStocks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medication_stocks_quantity_non_negative",
                table: "MedicationStocks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medication_stocks_reserved_within_quantity",
                table: "MedicationStocks");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "ParentStockId",
                table: "MedicationStocks");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "MedicationStocks");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MedicationStocks");

            migrationBuilder.RenameColumn(
                name: "MedicationActiveIngredientId",
                table: "Orders",
                newName: "MedicationBrandId");

            migrationBuilder.RenameColumn(
                name: "DispensedById",
                table: "Orders",
                newName: "ApprovedById");

            migrationBuilder.RenameColumn(
                name: "DispensedAt",
                table: "Orders",
                newName: "ApprovedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_MedicationActiveIngredientId",
                table: "Orders",
                newName: "IX_Orders_MedicationBrandId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_DispensedById",
                table: "Orders",
                newName: "IX_Orders_ApprovedById");

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockChangeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => new { x.OrderId, x.StockChangeId });
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_StockChanges_StockChangeId",
                        column: x => x.StockChangeId,
                        principalTable: "StockChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StockChangeId",
                table: "OrderItems",
                column: "StockChangeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MedicationBrands_MedicationBrandId",
                table: "Orders",
                column: "MedicationBrandId",
                principalTable: "MedicationBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_StaffUsers_ApprovedById",
                table: "Orders",
                column: "ApprovedById",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
