using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WebCRM.DemoData;

/// <summary>
/// Copies entity objects into their table with SqlBulkCopy, keeping the ids the generator chose. The columns come
/// from the EF model, so a column added later is copied too. Constraints are checked (CheckConstraints), which keeps
/// them trusted and means a row that breaks a CHECK or a foreign key fails the whole run.
/// </summary>
public static class BulkWriter
{
    public const int ChunkSize = 10_000;

    public static async Task<int> WriteAsync<T>(
        SqlConnection connection, SqlTransaction transaction, IEntityType entityType, IReadOnlyList<T> rows, CancellationToken cancellationToken)
        where T : class
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var columns = entityType.GetProperties()
            .Where(p => p.PropertyInfo is not null
                && p.GetComputedColumnSql() is null
                && p.GetColumnType() != "rowversion"
                && !(p.IsConcurrencyToken && p.ClrType == typeof(byte[])))
            .Select(p => (Column: p.GetColumnName(), Property: p.PropertyInfo!))
            .ToList();

        var table = new DataTable();
        foreach (var (column, property) in columns)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            table.Columns.Add(column, type.IsEnum ? Enum.GetUnderlyingType(type) : type == typeof(DateOnly) ? typeof(DateTime) : type);
        }

        using var copy = new SqlBulkCopy(
            connection, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.TableLock, transaction)
        {
            DestinationTableName = $"[{entityType.GetSchema() ?? "dbo"}].[{entityType.GetTableName()}]",
            BatchSize = ChunkSize,
            BulkCopyTimeout = 0,
        };
        foreach (var (column, _) in columns)
        {
            copy.ColumnMappings.Add(column, column);
        }

        // Chunks keep the memory flat however many rows there are.
        for (var offset = 0; offset < rows.Count; offset += ChunkSize)
        {
            table.Clear();
            var end = Math.Min(offset + ChunkSize, rows.Count);
            for (var i = offset; i < end; i++)
            {
                var row = table.NewRow();
                for (var c = 0; c < columns.Count; c++)
                {
                    row[c] = Convert(columns[c].Property.GetValue(rows[i]));
                }

                table.Rows.Add(row);
            }

            await copy.WriteToServerAsync(table, cancellationToken);
        }

        return rows.Count;
    }

    private static object Convert(object? value) => value switch
    {
        null => DBNull.Value,
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        Enum e => System.Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType())),
        _ => value,
    };
}
