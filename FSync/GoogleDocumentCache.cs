namespace FSync
{
	using System;
	using System.Data;
	using System.Collections.Generic;
	using System.Data.Common;
	using System.Data.Entity;
	using System.Linq;
	using SQLite.CodeFirst;


	public class GoogleDocumentCache : DbContext
	{
		public Dictionary<string, GoogleDriveDocument> Documents { get; protected set; }
		public DbSet<GoogleDocumentIndex> DocumentIndex { get; set; }

		public GoogleDocumentCache(string connectionString) : base(connectionString) {
			Documents = new Dictionary<string, GoogleDriveDocument>();
		}
		public GoogleDocumentCache(DbConnection db) : base(db, true) {
			Documents = new Dictionary<string, GoogleDriveDocument>();
		}

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<GoogleDocumentCache>(modelBuilder);
			Database.SetInitializer(sqliteConnectionInitializer);
		}

		public void Add(IDocument document)
		{
			var index = new GoogleDocumentIndex {
				Id = document.Id,
				Parent = document.Parent.Id,
				Name = document.Name,
				Version = document.Version
			};
			if (DocumentIndex.Any(i => i.Id == document.Id)) {
				DocumentIndex.Attach(index);
				Entry(index).State = EntityState.Modified;
			} else {
				DocumentIndex.Add(index);
			}

			//Documents.Add(document.Id, document);
			SaveChanges();
		}

		public bool ContainsDocument(string id)
		{
			return Documents.ContainsKey(id);
		}
	}

	public class GoogleDocumentIndex
	{
		public string Id { get; set; }
		public string Parent { get; set; }
		public string Name { get; set; }
		public long Version { get; set; }
	}
}