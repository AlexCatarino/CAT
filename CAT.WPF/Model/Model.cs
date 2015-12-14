namespace CAT.WPF.Model
{
    using CAT.Model;
    using System.Data.Entity;

    public class DatabaseContext : DbContext
    {
        // Your context has been configured to use a 'Model' connection string from your application's 
        // configuration file (App.config or Web.config). By default, this connection string targets the 
        // 'CAT.WPF.Model.Model' database on your LocalDb instance. 
        // 
        // If you wish to target a different database and/or database provider, modify the 'Model' 
        // connection string in the application configuration file.
        public DatabaseContext()
            : base("name=DatabaseContext")
        {
        }

        // Add a DbSet for each entity type that you want to include in your model. For more information 
        // on configuring and using a Code First model, see http://go.microsoft.com/fwlink/?LinkId=390109.

        // public virtual DbSet<MyEntity> MyEntities { get; set; }
        public virtual DbSet<Setup> Setups { get; set; }
        public virtual DbSet<Client> Clients { get; set; }
        //public virtual DbSet<UserInfo> Users { get; set; }
    }
}