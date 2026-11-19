using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PandaHunt.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<GameSettings> GameSettings => Set<GameSettings>();

    public DbSet<PointEntry> PointEntries => Set<PointEntry>();

    public DbSet<HuntTask> HuntTasks => Set<HuntTask>();

    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();

    public DbSet<TaskStar> TaskStars => Set<TaskStar>();

    public DbSet<FloorCode> FloorCodes => Set<FloorCode>();

    public DbSet<FloorCodeClaim> FloorCodeClaims => Set<FloorCodeClaim>();

    public DbSet<BonusEvent> BonusEvents => Set<BonusEvent>();

    public DbSet<BonusEventClaim> BonusEventClaims => Set<BonusEventClaim>();

    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

    public DbSet<WebPushDevice> WebPushDevices => Set<WebPushDevice>();

    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GameSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.Phase0Code).HasMaxLength(Data.GameSettings.MaxCodeLength);
            settings.Property(s => s.HuntIntro).HasMaxLength(Data.GameSettings.MaxHuntIntroLength);
            settings.Property(s => s.QrBaseUrl).HasMaxLength(Data.GameSettings.MaxQrBaseUrlLength);
        });

        builder.Entity<NotificationSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.SmtpHost).HasMaxLength(Data.NotificationSettings.MaxTextLength);
            settings.Property(s => s.SmtpUser).HasMaxLength(Data.NotificationSettings.MaxTextLength);
            settings.Property(s => s.SmtpFromAddress).HasMaxLength(Data.NotificationSettings.MaxTextLength);
            settings.Property(s => s.SmtpFromName).HasMaxLength(Data.NotificationSettings.MaxTextLength);
        });

        builder.Entity<WebPushDevice>(device =>
        {
            device.Property(d => d.Endpoint).HasMaxLength(WebPushDevice.MaxEndpointLength);
            device.Property(d => d.P256dh).HasMaxLength(WebPushDevice.MaxKeyLength);
            device.Property(d => d.Auth).HasMaxLength(WebPushDevice.MaxKeyLength);
            device.Property(d => d.Label).HasMaxLength(WebPushDevice.MaxLabelLength);
            device.HasIndex(d => d.Endpoint).IsUnique();
            device.HasOne(d => d.User)
                .WithMany(u => u.PushDevices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SentNotification>(sent =>
        {
            sent.HasKey(s => s.Key);
            sent.Property(s => s.Key).HasMaxLength(SentNotification.MaxKeyLength);
        });

        builder.Entity<Player>(player =>
        {
            player.Property(p => p.Name).HasMaxLength(Player.MaxNameLength);
        });

        // A login can't be left without a player; a player's data goes with it when the player is deleted (cascade below).
        builder.Entity<ApplicationUser>(user =>
        {
            user.HasOne(u => u.Player)
                .WithMany(p => p.Members)
                .HasForeignKey(u => u.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
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

        builder.Entity<TaskStar>(star =>
        {
            star.HasKey(s => new { s.TaskId, s.Color });
            star.HasOne(s => s.Task)
                .WithMany()
                .HasForeignKey(s => s.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            star.HasOne(s => s.Player)
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FloorCode>(code =>
        {
            code.Property(c => c.Token).HasMaxLength(FloorCode.TokenLength);
            code.HasIndex(c => c.Token).IsUnique();
            code.Property(c => c.Name).HasMaxLength(FloorCode.MaxNameLength);
        });

        builder.Entity<FloorCodeClaim>(claim =>
        {
            claim.HasKey(c => new { c.CodeId, c.PlayerId });
            claim.HasOne(c => c.Code)
                .WithMany()
                .HasForeignKey(c => c.CodeId)
                .OnDelete(DeleteBehavior.Cascade);
            claim.HasOne(c => c.Player)
                .WithMany()
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BonusEvent>(bonusEvent =>
        {
            bonusEvent.Property(e => e.Name).HasMaxLength(BonusEvent.MaxNameLength);
            bonusEvent.Property(e => e.Description).HasMaxLength(BonusEvent.MaxDescriptionLength);
        });

        builder.Entity<BonusEventClaim>(claim =>
        {
            claim.HasKey(c => new { c.EventId, c.PlayerId });
            claim.HasOne(c => c.Event)
                .WithMany()
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            claim.HasOne(c => c.Player)
                .WithMany()
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
