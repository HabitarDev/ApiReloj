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

    public Reloj? GetById(string id)
    {
        return _context.Relojes.FirstOrDefault(x => x.IdReloj == id);
    }

    public List<Reloj> GetAll()
    {
        return _context.Relojes.ToList();
    }

    public List<Reloj> GetPollCandidates(string? residentialId = null, string? relojId = null)
    {
        var query = _context.Relojes
            .Include(x => x.Residential)
            .AsQueryable();

        if (!string.IsNullOrEmpty(residentialId))
        {
            query = query.Where(x => x.ResidentialId == residentialId);
        }

        if (!string.IsNullOrEmpty(relojId))
        {
            query = query.Where(x => x.IdReloj == relojId);
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

    public void delete(string id)
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
