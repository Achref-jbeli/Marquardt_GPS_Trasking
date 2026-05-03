using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpsAdminServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chauffeurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChauffeurId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotDePasse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumeroSerie = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Marque = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Modele = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Annee = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrackingStarted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrackingStopped = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogout = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chauffeurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Missions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChauffeurId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Client = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Heure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Missions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChauffeurId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    SpeedKmh = table.Column<double>(type: "float", nullable: false),
                    AltitudeM = table.Column<double>(type: "float", nullable: false),
                    AccuracyM = table.Column<double>(type: "float", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MapsLink = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chauffeurs_ChauffeurId",
                table: "Chauffeurs",
                column: "ChauffeurId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Timestamp",
                table: "Positions",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chauffeurs");

            migrationBuilder.DropTable(
                name: "Missions");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
