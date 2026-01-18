using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ArtMatch.Entities;
namespace ArtMatch;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Entities.User> Users { get; set; }
    public DbSet<Entities.Photo> Photos { get; set; }
}
