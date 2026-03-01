using DataAcces.Context;
using Dominio;
using IDataAcces;
using Microsoft.EntityFrameworkCore;

namespace DataAcces.Repositories;

public class RelojesRepository (SqlContext repos) : IRelojesRepository
{
    private readonly SqlContext _context = repos;
    public Reloj Add(Reloj reloj)
    {
        _context.Relojes.Add(reloj);
        _context.SaveChanges();
        return reloj;
    }

    public Reloj? GetById(int id)
    {
        return _context.Relojes.FirstOrDefault(x => x.IdReloj == id);
    }

    public List<Reloj> GetAll()
    {
        return _context.Relojes.ToList();
    }

    public List<Reloj> GetPollCandidates(int? residentialId = null, int? relojId = null)
    {
        var query = _context.Relojes
            .Include(x => x.Residential)
            .AsQueryable();

        if (residentialId.HasValue)
        {
            query = query.Where(x => x.ResidentialId == residentialId.Value);
        }

        if (relojId.HasValue)
        {
            query = query.Where(x => x.IdReloj == relojId.Value);
        }

        return query.ToList();
    }

    public void update(Reloj reloj)
    {
        var exists = _context.Relojes.Any(x => x.IdReloj == reloj.IdReloj);
        if(!exists)
        {
            throw new InvalidOperationException("Atracción inexistente");
        }

        _context.Relojes.Update(reloj);
        _context.SaveChanges();
    }

    public void delete(int id)
    {
        var reloj = GetById(id);
        if(reloj == null)
        {
            throw new InvalidOperationException("Atracción inexistente");
        }

        _context.Relojes.Remove(reloj);
        _context.SaveChanges();
    }
}
