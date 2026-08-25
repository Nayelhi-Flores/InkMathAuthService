using System.Data;
using Microsoft.Data.SqlClient;

namespace InkMath.AuthService.Data
{
    public interface ISeedRepository
    {
        Task SeedTransaccionesBulkAsync(int cantidadTotal, long estudianteIdDefault);
    }

    public class SeedRepository : ISeedRepository
    {
        private readonly string _connectionString;

        public SeedRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("SqlServerConnection")!;
        }

        public async Task SeedTransaccionesBulkAsync(int cantidadTotal, long estudianteIdDefault)
        {
            var table = new DataTable();
            table.Columns.Add("estudiante_id", typeof(long));
            table.Columns.Add("tipo_transaccion_id", typeof(long));
            table.Columns.Add("monto", typeof(long));
            table.Columns.Add("idempotency_key", typeof(string));
            table.Columns.Add("creado_en", typeof(DateTimeOffset));

            var random = new Random();
            var fechasDisponibles = new[]
            {
                new DateTimeOffset(2024, 05, 10, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 08, 15, 14, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 02, 20, 09, 15, 0, TimeSpan.Zero)
            };

            for (int i = 0; i < cantidadTotal; i++)
            {
                var fechaBase = fechasDisponibles[random.Next(fechasDisponibles.Length)];
                var fecha = fechaBase.AddHours(random.Next(0, 24)).AddMinutes(random.Next(0, 60));

                table.Rows.Add(
                    estudianteIdDefault,
                    random.Next(1, 3), // Suponiendo tipos de transacción 1 o 2 (Ej. Recompensa o Compra)
                    random.Next(10, 500),
                    Guid.NewGuid().ToString(),
                    fecha
                );
            }

            using var bulkCopy = new SqlBulkCopy(_connectionString)
            {
                DestinationTableName = "dbo.transacciones_monedas",
                BatchSize = 10000,
                BulkCopyTimeout = 120
            };

            bulkCopy.ColumnMappings.Add("estudiante_id", "estudiante_id");
            bulkCopy.ColumnMappings.Add("tipo_transaccion_id", "tipo_transaccion_id");
            bulkCopy.ColumnMappings.Add("monto", "monto");
            bulkCopy.ColumnMappings.Add("idempotency_key", "idempotency_key");
            bulkCopy.ColumnMappings.Add("creado_en", "creado_en");

            await bulkCopy.WriteToServerAsync(table);
        }
    }
}
