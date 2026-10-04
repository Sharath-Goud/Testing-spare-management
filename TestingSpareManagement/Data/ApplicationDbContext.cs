using Microsoft.EntityFrameworkCore;
using TestingSpareManagement.Models;

namespace TestingSpareManagement.Data
{
    public class ApplicationDbContext : DbContext
    {

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }
        public DbSet<User> Users { get; set; }

    }
}