using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Code-first model that mirrors the Part 1 schema (docs/schema.sql): same table names, column names,
/// column types, unique constraints and check constraints. Events carries two Part 2 additions
/// (EventType, DistanceKM) - see Database/part2-schema-additions.sql.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<RaceRoute> Routes => Set<RaceRoute>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<EventResult> Results => Set<EventResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("UserID");
            e.Property(x => x.Role).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();
            e.Property(x => x.FirstName).HasColumnType("varchar(50)").IsRequired();
            e.Property(x => x.LastName).HasColumnType("varchar(50)").IsRequired();
            e.Property(x => x.Email).HasColumnType("varchar(100)").IsRequired();
            e.Property(x => x.PasswordHash).HasColumnType("varchar(255)").IsRequired();
            e.Property(x => x.PhoneNumber).HasColumnType("varchar(20)");
            e.Property(x => x.ProfileImageUrl).HasColumnType("varchar(1000)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime");
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("EventID");
            e.Property(x => x.OrganiserId).HasColumnName("OrganiserID");
            e.Property(x => x.EventName).HasColumnType("varchar(100)").IsRequired();
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasColumnType("varchar(150)").IsRequired();
            e.Property(x => x.Description).HasColumnType("varchar(8000)");
            e.Property(x => x.BannerImageUrl).HasColumnType("varchar(1000)");
            e.Property(x => x.WeatherLocationCode).HasColumnType("varchar(20)");
            e.Property(x => x.EventType).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();
            e.Property(x => x.DistanceKM).HasColumnType("decimal(6,2)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime");

            e.HasOne(x => x.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RaceRoute>(e =>
        {
            e.ToTable("Routes");
            e.HasKey(x => x.RouteId);
            e.Property(x => x.RouteId).HasColumnName("RouteID");
            e.Property(x => x.EventId).HasColumnName("EventID");
            e.Property(x => x.RouteName).HasColumnType("varchar(100)").IsRequired();
            e.Property(x => x.DistanceKM).HasColumnType("decimal(6,2)");
            e.Property(x => x.ElevationGainM);
            e.Property(x => x.MapURL).HasColumnName("MapURL").HasColumnType("varchar(255)");

            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Routes)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Routes_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryId).HasColumnName("CategoryID");
            e.Property(x => x.EventId).HasColumnName("EventID");
            e.Property(x => x.CategoryName).HasColumnType("varchar(50)").IsRequired();
            e.Property(x => x.DistanceKM).HasColumnType("decimal(6,2)");
            e.Property(x => x.MaxParticipants);
            e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)");

            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Categories_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint(
                "CK_Enrolments_Status", "[Status] IN ('Confirmed', 'Cancelled', 'Pending')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(x => x.ParticipantId).HasColumnName("ParticipantID");
            e.Property(x => x.CategoryId).HasColumnName("CategoryID");
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime");
            e.Property(x => x.BibNumber).HasColumnType("varchar(10)");
            e.Property(x => x.Status).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();

            // UQ_Enrolments_Participant_Category from the Part 1 script.
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId })
                .IsUnique()
                .HasDatabaseName("UQ_Enrolments_Participant_Category");

            e.HasOne(x => x.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .HasConstraintName("FK_Enrolments_Category")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventResult>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint(
                "CK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DSQ')"));
            e.HasKey(x => x.ResultId);
            e.Property(x => x.ResultId).HasColumnName("ResultID");
            e.Property(x => x.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.Status).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();

            // EnrolmentID is UNIQUE in the Part 1 script: one result per enrolment.
            e.HasIndex(x => x.EnrolmentId).IsUnique();

            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<EventResult>(x => x.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolment")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
