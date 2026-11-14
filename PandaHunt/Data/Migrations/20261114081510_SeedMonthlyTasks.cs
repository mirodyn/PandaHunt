using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <summary>
    /// Creates the 12 monthly tasks (January–December 2027), all of the "not decided yet" type and without
    /// a page. Runs once per database; afterwards everything is edited in Administrace → Úkoly.
    /// </summary>
    public partial class SeedMonthlyTasks : Migration
    {
        private static readonly string[] Months =
        [
            "Leden", "Únor", "Březen", "Duben", "Květen", "Červen",
            "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            for (var i = 0; i < Months.Length; i++)
            {
                var month = i + 1;
                // Midnight Prague time on the 1st. Summer time in 2027 runs from 28 March to 31 October,
                // so the 1st of April–October is UTC+2 and the other months UTC+1.
                var offset = TimeSpan.FromHours(month is >= 4 and <= 10 ? 2 : 1);

                migrationBuilder.InsertData(
                    table: "HuntTasks",
                    columns: ["Name", "Type", "OpensAt"],
                    columnTypes: ["TEXT", "TEXT", "TEXT"],
                    values: new object[] { Months[i], "neznamy", new DateTimeOffset(2027, month, 1, 0, 0, 0, offset) });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM HuntTasks WHERE Type = 'neznamy' AND Link IS NULL AND Name IN ("
                + string.Join(", ", Months.Select(m => $"'{m}'")) + ");");
        }
    }
}
