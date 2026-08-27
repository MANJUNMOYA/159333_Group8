using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CampusCoffeeSystem.Models.UserActivity> UserActivities => Set<CampusCoffeeSystem.Models.UserActivity>();
        public DbSet<CampusCoffeeSystem.Models.AiChatMessage> AiChatMessages => Set<CampusCoffeeSystem.Models.AiChatMessage>();
    }
}
