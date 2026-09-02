namespace IDataAcces;

public interface IDataTransaction : IDisposable
{
    void Commit();
    void Rollback();
}

public interface IDataTransactionManager
{
    IDataTransaction BeginTransaction();
    void ClearTracking();
}
