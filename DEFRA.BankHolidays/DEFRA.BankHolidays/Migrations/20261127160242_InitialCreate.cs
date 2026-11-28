using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DEFRA.BankHolidays.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnglandWales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglandWales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NorthernIreland",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NorthernIreland", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scotland",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scotland", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Event",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bunting = table.Column<bool>(type: "bit", nullable: false),
                    EnglandWales_Id = table.Column<int>(type: "int", nullable: true),
                    NorthernIreland_Id = table.Column<int>(type: "int", nullable: true),
                    Scotland_Id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Event_EnglandWales_EnglandWales_Id",
                        column: x => x.EnglandWales_Id,
                        principalTable: "EnglandWales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Event_NorthernIreland_NorthernIreland_Id",
                        column: x => x.NorthernIreland_Id,
                        principalTable: "NorthernIreland",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Event_Scotland_Scotland_Id",
                        column: x => x.Scotland_Id,
                        principalTable: "Scotland",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglandWales_Id = table.Column<int>(type: "int", nullable: true),
                    Scotland_Id = table.Column<int>(type: "int", nullable: true),
                    NorthernIreland_Id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_EnglandWales_EnglandWales_Id",
                        column: x => x.EnglandWales_Id,
                        principalTable: "EnglandWales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Holidays_NorthernIreland_NorthernIreland_Id",
                        column: x => x.NorthernIreland_Id,
                        principalTable: "NorthernIreland",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Holidays_Scotland_Scotland_Id",
                        column: x => x.Scotland_Id,
                        principalTable: "Scotland",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Event_EnglandWales_Id",
                table: "Event",
                column: "EnglandWales_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Event_NorthernIreland_Id",
                table: "Event",
                column: "NorthernIreland_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Event_Scotland_Id",
                table: "Event",
                column: "Scotland_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_EnglandWales_Id",
                table: "Holidays",
                column: "EnglandWales_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_NorthernIreland_Id",
                table: "Holidays",
                column: "NorthernIreland_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Scotland_Id",
                table: "Holidays",
                column: "Scotland_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Event");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "EnglandWales");

            migrationBuilder.DropTable(
                name: "NorthernIreland");

            migrationBuilder.DropTable(
                name: "Scotland");
        }
    }
}
