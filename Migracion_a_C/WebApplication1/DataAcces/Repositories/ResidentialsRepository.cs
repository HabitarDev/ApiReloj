using DataAcces.Context;
using Dominio;
using IDataAcces;
using Microsoft.EntityFrameworkCore;

namespace DataAcces.Repositories;

public class ResidentialsRepository (SqlContext repos) : IResidentialsRepository
{
    private readonly SqlContext _context = repos;
    public Residential Add(Residential residential)
    {
        _context.Residentials.Add(residential);
        _context.SaveChanges();
        return residential;
    }

    public Residential? GetById(int id)
    {
        return _context.Residentials
            .Include(x => x.Relojes)
            .Include(x => x.Devices)
            .FirstOrDefault(x => x.IdResidential == id);
    }

    public List<Residential> GetAll()
    {
        return _context.Residentials.ToList();
    }

    public void update(Residential residential)
    {
        var exists = _context.Relojes.Any(x => x.IdReloj == residential.IdResidential);
        if(!exists)
        {
            throw new InvalidOperationException("Atracción inexistente");
        }

        _context.Residentials.Update(residential);
        _context.SaveChanges();
    }

    public void delete(int id)
    {
        var res = GetById(id);
        if(res == null)
        {
            throw new InvalidOperationException("Atracción inexistente");
        }

        _context.Residentials.Remove(res);
        _context.SaveChanges();
    }

    public bool IsMine(int id)
    {
        throw new NotImplementedException();
    }
}
