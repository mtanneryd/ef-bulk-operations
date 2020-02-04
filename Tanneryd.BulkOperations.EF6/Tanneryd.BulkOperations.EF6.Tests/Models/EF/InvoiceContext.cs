/*
 * Copyright ©  2017-2019 Tånneryd IT AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Tanneryd.BulkOperations.EF6.Tests.DM.Invoice;

namespace Tanneryd.BulkOperations.EF6.Tests.EF
{


    public class InvoiceContext : DbContext
    {
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Journal> Journals { get; set; }
        public DbSet<BatchInvoice> BatchInvoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<BatchInvoiceItem> BatchInvoiceItems { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BatchInvoiceItem>()
                .ToTable("BatchInvoiceItem")
                .HasKey(p => p.Id);
            modelBuilder.Entity<BatchInvoiceItem>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");

            modelBuilder.Entity<InvoiceItem>()
                .ToTable("InvoiceItem")
                .HasKey(p => p.Id);
            modelBuilder.Entity<InvoiceItem>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<InvoiceItem>()
                .Property(p => p.Id)
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            modelBuilder.Entity<Invoice>()
                .ToTable("Invoice")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Invoice>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<Invoice>()
                .HasMany(b => b.BatchInvoices)
                .WithRequired(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId);
            modelBuilder.Entity<Invoice>()
                .HasMany(b => b.Journals)
                .WithRequired(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId);

            modelBuilder.Entity<BatchInvoice>()
                .ToTable("BatchInvoice")
                .HasKey(p => p.Id);
            modelBuilder.Entity<BatchInvoice>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<BatchInvoice>()
                .HasMany(p => p.Invoices)
                .WithRequired(k => k.BatchInvoice)
                .HasForeignKey(k => k.BatchInvoiceId);

            modelBuilder.Entity<Journal>()
                .ToTable("Journal")
                .HasKey(p => p.Id);
            modelBuilder.Entity<Journal>()
                .Property(p => p.Id)
                .HasColumnName("PrimaryKey");
            modelBuilder.Entity<Journal>()
                .HasMany(p => p.Invoices)
                .WithRequired(k => k.Journal)
                .HasForeignKey(k => k.JournalId);
        }
    }
}