using Microsoft.EntityFrameworkCore;

namespace WebCRM.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
