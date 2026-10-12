using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

public static class QueryableExtensions
{
    public static async Task<T> FirstOrNotFoundAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        string message,
        CancellationToken cancellationToken
    )
    {
        return await query.FirstOrDefaultAsync(predicate, cancellationToken)
            ?? throw new NotFoundException(message);
    }
}
