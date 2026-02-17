using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PurviewConsortium.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConsortiumDbContext>
{
    public ConsortiumDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConsortiumDbContext>();
        // Use a dummy connection string for migration generation only
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PurviewConsortium_Design;Trusted_Connection=True;");
        return new ConsortiumDbContext(optionsBuilder.Options);
    }
}
