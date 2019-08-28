using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Misaka.Interfaces;
using System;

namespace Misaka.Classes
{
    public class DBContextBase : DbContext, IDBContext
    {
        public string ConnectionString { get; set; }

        public DBContextBase() { }

        public DBContextBase(DbContextOptions<DBContextBase> opts) : base(opts)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseMySql(ConnectionString);
            var builder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());

            OnModelCreating(builder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            BuildModels(modelBuilder);
        }

        protected virtual void BuildModels(ModelBuilder modelBuilder) { }
    }
}
