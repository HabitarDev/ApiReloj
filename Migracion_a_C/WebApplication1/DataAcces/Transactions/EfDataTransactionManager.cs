using DataAcces.Context;
using IDataAcces;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataAcces.Transactions;

public class EfDataTransactionManager(SqlContext context) : IDataTransactionManager
{
    private readonly SqlContext _context = context;

    public IDataTransaction BeginTransaction()
    {
        return new EfDataTransaction(_context.Database.BeginTransaction());
    }

    public void ClearTracking()
    {
        _context.ChangeTracker.Clear();
    }

    private sealed class EfDataTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        private readonly IDbContextTransaction _transaction = transaction;

        public void Commit() => _transaction.Commit();
        public void Rollback() => _transaction.Rollback();
        public void Dispose() => _transaction.Dispose();
    }
}
