<Query Kind="Program">
  <Connection>
    <ID>34224e53-1e3f-45b1-b2a2-37e09df890f2</ID>
    <Persist>true</Persist>
    <Driver>EntityFrameworkDbContext</Driver>
    <CustomAssemblyPath>C:\repos\ef6-bulk-operations\Tanneryd.BulkOperations.EF6\Tanneryd.BulkOperations.EF6.Tests\bin\Debug\Tanneryd.BulkOperations.EF6.Tests.dll</CustomAssemblyPath>
    <CustomTypeName>Tanneryd.BulkOperations.EF6.Tests.EF.InstrumentContext</CustomTypeName>
    <AppConfigPath>C:\repos\ef6-bulk-operations\Tanneryd.BulkOperations.EF6\Tanneryd.BulkOperations.EF6.Tests\bin\Debug\Tanneryd.BulkOperations.EF6.Tests.dll.config</AppConfigPath>
    <DisplayName>InstrumentContext</DisplayName>
  </Connection>
  <Output>DataGrids</Output>
  <Reference Relative="Tanneryd.BulkOperations.EF6\bin\Debug\Tanneryd.BulkOperations.EF6.dll">C:\repos\ef6-bulk-operations\Tanneryd.BulkOperations.EF6\Tanneryd.BulkOperations.EF6\bin\Debug\Tanneryd.BulkOperations.EF6.dll</Reference>
  <Namespace>Tanneryd.BulkOperations.EF6.Tests.DM.Instruments</Namespace>
</Query>

void Main()
{	
	GenerateInstruments();
	Expression<Func<Instrument, bool>> predicate = i => i.Currency.Id == "SEK" && i.Name == "ERIC B";
	var db = new InstrumentContext();
	db.Instruments
	.Where(predicate)
	.ToString().Dump();	
}

// Define other methods and classes here
public void GenerateInstruments()
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