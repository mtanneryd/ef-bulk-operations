/*
* Copyright ©  2017-2018 Tånneryd IT AB
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