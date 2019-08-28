using Microsoft.EntityFrameworkCore;
using Misaka.Models.MySQL;
using Misaka.Interfaces;

namespace Misaka.Classes
{
    public interface IDiscordObject
    {
        string Id
        {
            get;
            set;
        }
    }

    public class BotDBContext : DBContextBase, IDBContext
    {
        public BotDBContext() { }

        public BotDBContext(DbContextOptions<DBContextBase> opts) : base(opts)
        {
        }

        /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(ConnectionString + "CharSet=utf32");
            var builder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());

            OnModelCreating(builder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DiscordMessageAttachment>()
                .HasKey(x => new { x.Id, x.Url });
            modelBuilder.Entity<DiscordReaction>()
                .HasKey(x => new { x.Id, x.ReceiverId, x.ReactorId, x.ReactionName });
            modelBuilder.Entity<DiscordGameTime>()
                .HasKey(x => new { x.Id, x.Name });
            modelBuilder.Entity<DiscordCommandLog>()
                .HasKey(x => new { x.Id, x.ExecutorId, x.GuildId, x.ChannelId });
            modelBuilder.Entity<DiscordSalutationMessage>()
                .HasKey(x => new { x.Id, x.OnJoin });
            modelBuilder.Entity<DiscordCustomPrefix>()
                .HasKey(x => new { x.Id, x.CreatorId, x.Prefix });
        }*/

        protected override void BuildModels(ModelBuilder modelBuilder)
        {
            base.BuildModels(modelBuilder);

            modelBuilder.Entity<DiscordMessageAttachment>()
                .HasKey(x => new { x.Id, x.Url });
            modelBuilder.Entity<DiscordReaction>()
                .HasKey(x => new { x.Id, x.ReceiverId, x.ReactorId, x.ReactionName });
            modelBuilder.Entity<DiscordGameTime>()
                .HasKey(x => new { x.Id, x.Name });
            modelBuilder.Entity<DiscordCommandLog>()
                .HasKey(x => new { x.Id, x.ExecutorId, x.GuildId, x.ChannelId });
            modelBuilder.Entity<DiscordSalutationMessage>()
                .HasKey(x => new { x.Id, x.OnJoin });
            modelBuilder.Entity<DiscordCustomPrefix>()
                .HasKey(x => new { x.Id, x.CreatorId, x.Prefix });
        }

        public DbSet<DiscordUser> Users { get; set; }
        public DbSet<DiscordGuild> Guilds { get; set; }
        public DbSet<DiscordTextChannel> TextChannels { get; set; }
        public DbSet<DiscordMessage> Messages { get; set; }
        public DbSet<DiscordMessageAttachment> Attachments { get; set; }
        public DbSet<DiscordReaction> Reactions { get; set; }
        public DbSet<DiscordGameTime> GameTime { get; set; }
        public DbSet<ChannelTag> ChannelTags { get; set; }
        public DbSet<DiscordCommandLog> CommandLogs { get; set; }
        public DbSet<DiscordSalutationMessage> DiscordSalutationMessages { get; set; }
        public DbSet<DiscordCustomPrefix> DiscordCustomPrefixes { get; set; }
    }
}
