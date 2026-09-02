using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaProjectionService
{
    bool ProcessNext(DateTimeOffset nowUtc);
    int EnqueueExpired(DateTimeOffset nowUtc);
    void RequestRebuild(JornadaRebuildRequestDto request);
    List<JornadaProjectionStateDto> GetStates(string? status, int limit, int offset);
}
