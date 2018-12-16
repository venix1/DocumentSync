using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;

namespace DocumentSync.Backend.Google {
    public class GoogleDocumentCache : DbContext {
        public Dictionary<string, string> IdPath { get; protected set; }
        public Dictionary<string, GoogleDriveDocument> Documents { get; protected set; }
        public DbSet<GoogleDocumentIndex> DocumentIndex { get; set; }

        public GoogleDocumentCache() {
            Initialize();
        }
        private void Initialize() {
            Documents = new Dictionary<string, GoogleDriveDocument>();
            IdPath = new Dictionary<string, string>();
            Database.EnsureCreated();
            SaveChanges();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            optionsBuilder.UseSqlite("Data Source=google_drive.db");

        }

        public bool TryGetValue(string key, out GoogleDriveDocument value) {
            return Documents.TryGetValue(key, out value);
        }

        public bool Remove(string key) {
            return Documents.Remove(key);
        }

        public void Add(IDocument document) {
            try {
                Documents.Add(document.Id, (GoogleDriveDocument)document);
            }
            catch (System.ArgumentException e) {
                Console.WriteLine("{0} {1}", document.Id, document.FullName);
                throw e;
            }
            return;
            GoogleDocumentIndex index;
            lock (this) {

                index = DocumentIndex.SingleOrDefault(i => i.Id == document.Id);

                if (index == null) {
                    index = new GoogleDocumentIndex();

                    DocumentIndex.Add(index);
                    Documents.Add(document.Id, (GoogleDriveDocument)document);
                }

                index.Id = document.Id;
                index.Parent = document.Parent?.Id;
                index.Name = document.Name;
                index.Version = document.Version;

                SaveChanges();
            }
        }

        public bool ContainsDocument(string id) {
            return Documents.ContainsKey(id);
        }
    }

    public class GoogleDocumentIndex {
        public string Id { get; set; }
        public string Parent { get; set; }
        public string Name { get; set; }
        public long Version { get; set; }
    }
}