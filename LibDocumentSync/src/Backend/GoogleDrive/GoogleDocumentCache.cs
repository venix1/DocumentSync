namespace DocumentSync
{
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Linq;

    public class GoogleDocumentCache : DbContext
    {
        public Dictionary<string, GoogleDriveDocument> Documents { get; protected set; }
        public DbSet<GoogleDocumentIndex> DocumentIndex { get; set; }

        private void Initialize()
        {
            Documents = new Dictionary<string, GoogleDriveDocument>();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=goole_drive.db");
        }


        public void Add(IDocument document)
        {
            GoogleDocumentIndex index;

            index = DocumentIndex.SingleOrDefault(i => i.Id == document.Id);

            if (index == null)
            {
                index = new GoogleDocumentIndex();
                DocumentIndex.Add(index);
                Documents.Add(document.Id, (GoogleDriveDocument)document);
            }

            index.Id = document.Id;
            index.Parent = document.Parent.Id;
            index.Name = document.Name;
            index.Version = document.Version;

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