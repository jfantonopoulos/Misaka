using Microsoft.EntityFrameworkCore;
using Misaka.Interfaces;
using Misaka.Models.MySQL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Classes
{
    class SubDBContext : DBContextBase, IDBContext
    {
        public SubDBContext() { }

        public SubDBContext(DbContextOptions<DBContextBase> opts) : base(opts)
        {
        }

        protected override void BuildModels(ModelBuilder modelBuilder)
        {
            base.BuildModels(modelBuilder);

            modelBuilder.Entity<RedditSubscriber>()
                .HasKey(x => new { x.Id, x.Subscriber, x.Subreddit });
            modelBuilder.Entity<RedditSubscriber>().ToTable("RedditSubscribers");
            modelBuilder.Entity<CNNSubscriber>().ToTable("CNNSubscribers");
        }

        public DbSet<RedditSubscriber> RedditSubscribers { get; set; }
        public DbSet<CNNSubscriber> CNNSubscribers { get; set; }
    }
}
