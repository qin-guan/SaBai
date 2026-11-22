using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaBai.API.Migrations
{
    /// <inheritdoc />
    public partial class ReserveStockPerOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders now belong to an active ingredient; the brand becomes an optional pin.
            migrationBuilder.AddColumn<Guid>(
                name: "MedicationActiveIngredientId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Orders" o
                SET "MedicationActiveIngredientId" = b."ActiveIngredientId"
                FROM "MedicationBrands" b
                WHERE b."Id" = o."MedicationBrandId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicationActiveIngredientId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicationBrandId",
                table: "Orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MedicationActiveIngredientId",
                table: "Orders",
                column: "MedicationActiveIngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MedicationActiveIngredients_MedicationActiveIngredie~",
                table: "Orders",
                column: "MedicationActiveIngredientId",
                principalTable: "MedicationActiveIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Order items now point at the lot itself (with the amount taken) instead of at the
            // stock-change row written on approval. Existing items keep their lot and quantity,
            // read back from that stock-change row.
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_StockChanges_StockChangeId",
                table: "OrderItems");

            migrationBuilder.AddColumn<Guid>(
                name: "StockId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "OrderItems" oi
                SET "StockId" = sc."StockId",
                    "Quantity" = CASE
                        WHEN sc."PreviousValue" ~ '^[0-9]+$' AND sc."NewValue" ~ '^[0-9]+$'
                            THEN GREATEST(sc."PreviousValue"::int - sc."NewValue"::int, 0)
                        ELSE 0
                    END
                FROM "StockChanges" sc
                WHERE sc."Id" = oi."StockChangeId";
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_StockChangeId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockChangeId",
                table: "OrderItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems",
                columns: new[] { "OrderId", "StockId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StockId",
                table: "OrderItems",
                column: "StockId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_MedicationStocks_StockId",
                table: "OrderItems",
                column: "StockId",
                principalTable: "MedicationStocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best effort. Order items go back to pointing at a stock-change row (the latest
            // quantity change of their lot); items whose lot has none are dropped. Orders that
            // never named a brand take the first brand of their ingredient.
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_MedicationStocks_StockId",
                table: "OrderItems");

            migrationBuilder.AddColumn<Guid>(
                name: "StockChangeId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "OrderItems" oi
                SET "StockChangeId" = (
                    SELECT sc."Id" FROM "StockChanges" sc
                    WHERE sc."StockId" = oi."StockId" AND sc."Field" = 'Quantity'
                    ORDER BY sc."CreatedAt" DESC LIMIT 1);
                DELETE FROM "OrderItems" WHERE "StockChangeId" IS NULL;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_StockId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "OrderItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockChangeId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems",
                columns: new[] { "OrderId", "StockChangeId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StockChangeId",
                table: "OrderItems",
                column: "StockChangeId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_StockChanges_StockChangeId",
                table: "OrderItems",
                column: "StockChangeId",
                principalTable: "StockChanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_MedicationActiveIngredients_MedicationActiveIngredie~",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_MedicationActiveIngredientId",
                table: "Orders");

            migrationBuilder.Sql(
                """
                UPDATE "Orders" o
                SET "MedicationBrandId" = (
                    SELECT b."Id" FROM "MedicationBrands" b
                    WHERE b."ActiveIngredientId" = o."MedicationActiveIngredientId"
                    ORDER BY b."Id" LIMIT 1)
                WHERE o."MedicationBrandId" IS NULL;
                DELETE FROM "Orders" WHERE "MedicationBrandId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicationBrandId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "MedicationActiveIngredientId",
                table: "Orders");
        }
    }
}
