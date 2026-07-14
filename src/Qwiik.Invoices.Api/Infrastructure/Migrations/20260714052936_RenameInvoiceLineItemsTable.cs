using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qwiik.Invoices.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameInvoiceLineItemsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItem_Invoices_InvoiceId",
                table: "InvoiceLineItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceLineItem",
                table: "InvoiceLineItem");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItem",
                newName: "InvoiceLineItems");

            migrationBuilder.RenameIndex(
                name: "IX_InvoiceLineItem_InvoiceId",
                table: "InvoiceLineItems",
                newName: "IX_InvoiceLineItems_InvoiceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceLineItems",
                table: "InvoiceLineItems",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                table: "InvoiceLineItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceLineItems",
                table: "InvoiceLineItems");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItems",
                newName: "InvoiceLineItem");

            migrationBuilder.RenameIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItem",
                newName: "IX_InvoiceLineItem_InvoiceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceLineItem",
                table: "InvoiceLineItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItem_Invoices_InvoiceId",
                table: "InvoiceLineItem",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
