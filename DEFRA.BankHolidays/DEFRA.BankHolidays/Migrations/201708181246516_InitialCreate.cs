namespace DEFRA.BankHolidays.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Holidays",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EnglandWales_Id = c.Int(),
                        NorthernIreland_Id = c.Int(),
                        Scotland_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.EnglandWales", t => t.EnglandWales_Id)
                .ForeignKey("dbo.NorthernIreland", t => t.NorthernIreland_Id)
                .ForeignKey("dbo.Scotland", t => t.Scotland_Id)
                .Index(t => t.EnglandWales_Id)
                .Index(t => t.NorthernIreland_Id)
                .Index(t => t.Scotland_Id);
            
            CreateTable(
                "dbo.EnglandWales",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Division = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Event",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Date = c.DateTime(nullable: false),
                        Notes = c.String(),
                        Bunting = c.Boolean(nullable: false),
                        EnglandWales_Id = c.Int(),
                        NorthernIreland_Id = c.Int(),
                        Scotland_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.EnglandWales", t => t.EnglandWales_Id)
                .ForeignKey("dbo.NorthernIreland", t => t.NorthernIreland_Id)
                .ForeignKey("dbo.Scotland", t => t.Scotland_Id)
                .Index(t => t.EnglandWales_Id)
                .Index(t => t.NorthernIreland_Id)
                .Index(t => t.Scotland_Id);
            
            CreateTable(
                "dbo.NorthernIreland",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Division = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Scotland",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Division = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Holidays", "Scotland_Id", "dbo.Scotland");
            DropForeignKey("dbo.Event", "Scotland_Id", "dbo.Scotland");
            DropForeignKey("dbo.Holidays", "NorthernIreland_Id", "dbo.NorthernIreland");
            DropForeignKey("dbo.Event", "NorthernIreland_Id", "dbo.NorthernIreland");
            DropForeignKey("dbo.Holidays", "EnglandWales_Id", "dbo.EnglandWales");
            DropForeignKey("dbo.Event", "EnglandWales_Id", "dbo.EnglandWales");
            DropIndex("dbo.Event", new[] { "Scotland_Id" });
            DropIndex("dbo.Event", new[] { "NorthernIreland_Id" });
            DropIndex("dbo.Event", new[] { "EnglandWales_Id" });
            DropIndex("dbo.Holidays", new[] { "Scotland_Id" });
            DropIndex("dbo.Holidays", new[] { "NorthernIreland_Id" });
            DropIndex("dbo.Holidays", new[] { "EnglandWales_Id" });
            DropTable("dbo.Scotland");
            DropTable("dbo.NorthernIreland");
            DropTable("dbo.Event");
            DropTable("dbo.EnglandWales");
            DropTable("dbo.Holidays");
        }
    }
}
