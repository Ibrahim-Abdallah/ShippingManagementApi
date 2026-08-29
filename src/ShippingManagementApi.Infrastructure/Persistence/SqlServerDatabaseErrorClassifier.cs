using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ShippingManagementApi.Infrastructure.Persistence;

internal static class SqlServerDatabaseErrorClassifier
{
    private const int DuplicateKey = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && IsUniqueConstraintViolationNumber(sqlException.Number))
                return true;
        }

        return false;
    }

    internal static bool IsUniqueConstraintViolationNumber(int errorNumber) =>
        errorNumber is DuplicateKey or UniqueConstraintViolation;
}
