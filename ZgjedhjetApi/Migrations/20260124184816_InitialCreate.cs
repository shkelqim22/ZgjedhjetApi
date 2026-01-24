using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZgjedhjetApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Zgjedhjet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kategoria = table.Column<int>(type: "int", nullable: false),
                    Komuna = table.Column<int>(type: "int", nullable: false),
                    Qendra_e_Votimit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vendvotimi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Partia = table.Column<int>(type: "int", nullable: false),
                    Vota = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zgjedhjet", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Zgjedhjet");
        }
    }
}
