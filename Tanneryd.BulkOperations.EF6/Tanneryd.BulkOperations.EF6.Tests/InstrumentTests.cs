using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.Tests.DM.Instruments;
using Tanneryd.BulkOperations.EF6.Tests.EF;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class InstrumentTests
    {
        [TestInitialize]
        public void Initialize()
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<InstrumentContext>());
            GenerateInstruments();
        }

        private void GenerateInstruments()
        {
            var db = new InstrumentContext();
            db.Instruments.RemoveRange(db.Instruments.ToArray());
            db.Currencies.RemoveRange(db.Currencies.ToArray());
            db.SaveChanges();

            var instruments = new[]
            {
                new Instrument
                {
                    Name = "ERIC B",
                    Currency = new Currency { Id = "SEK" }
                },
                new Instrument
                {
                    Name = "ERIC B",
                    Currency = new Currency { Id = "USD" }
                },
                new Instrument
                {
                    Name = "ABCD",
                    Currency = new Currency { Id = "NOK" }
                },
                new Instrument
                {
                    Name = "TSLA",
                    Currency = new Currency { Id = "USD" }
                },
            };


            db.Instruments.AddRange(instruments);
            db.SaveChanges();
        }
    }
}