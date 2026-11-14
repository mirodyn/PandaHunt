using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PandaHunt.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<GameSettings> GameSettings => Set<GameSettings>();

    public DbSet<PointEntry> PointEntries => Set<PointEntry>();

    public DbSet<HuntTask> HuntTasks => Set<HuntTask>();

    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GameSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.Phase0Code).HasMaxLength(Data.GameSettings.MaxCodeLength);
            settings.Property(s => s.HuntIntro).HasMaxLength(Data.GameSettings.MaxHuntIntroLength);
        });

        builder.Entity<Message>(message =>
        {
            message.Property(m => m.Text).HasMaxLength(Message.MaxLength);
            message.HasOne(m => m.Author)
                .WithMany()
                .HasForeignKey(m => m.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PointEntry>(entry =>
        {
            entry.Property(e => e.Comment).HasMaxLength(PointEntry.MaxCommentLength);
            entry.Property(e => e.Source).HasMaxLength(PointEntry.MaxSourceLength);
            entry.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HuntTask>(task =>
        {
            task.Property(t => t.Name).HasMaxLength(HuntTask.MaxNameLength);
            task.Property(t => t.Type).HasMaxLength(HuntTask.MaxTypeLength);
            task.Property(t => t.Link).HasMaxLength(HuntTask.MaxLinkLength);
        });

        builder.Entity<TaskCompletion>(completion =>
        {
            completion.HasKey(c => new { c.TaskId, c.PlayerId });
            completion.HasOne(c => c.Task)
                .WithMany()
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            completion.HasOne(c => c.Player)
                .WithMany()
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
