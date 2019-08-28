using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace Misaka.Classes
{
    public class DBContextFactory
    {
        //public string ConnectionString = $"server=localhost;database=discord;uid=misaka;pwd=j552213j;";
        public string ConnectionString = "";

        public DBContextFactory(IServiceProvider provider)
        {
            ConnectionString = provider.GetService<Config>().GetConnectionString();

            var baseType = typeof(DBContextBase);
            var derivedTypes = baseType.GetTypeInfo().Assembly.GetTypes().Where(x => x != baseType && baseType.IsAssignableFrom(x)).ToList();
            foreach(Type type in derivedTypes)
            {
                //Initiate each context on startup, creating the database if needed. 
                //If no database exists, one will be created but only the tables within the first context instantiated will be created.
                MethodInfo method = this.GetType().GetMethod("Create");
                MethodInfo genericMethod = method.MakeGenericMethod(type);
                object context = genericMethod.Invoke(this, null);
                (context as DBContextBase).Database.EnsureCreated();
                (context as DBContextBase).Dispose();
            }
        }

        public TContext Create<TContext>()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DBContextBase>();
            optionsBuilder.UseMySql(ConnectionString);
            var context = Activator.CreateInstance(typeof(TContext), new object[] { optionsBuilder.Options });
            (context as DBContextBase).ConnectionString = ConnectionString;
            return (TContext)Convert.ChangeType(context, typeof(TContext));
        }
    }
}
