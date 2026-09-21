using DEFRA.BankHolidays.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Web;

namespace DEFRA.BankHolidays.DAL
{
    public class HolidayContext:DbContext
    {
        public virtual DbSet<Holidays> Holidays { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}