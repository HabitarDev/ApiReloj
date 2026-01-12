using DataAcces.Context;
using Dominio;
using IDataAcces;

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