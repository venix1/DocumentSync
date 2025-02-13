﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DocumentSync.Test {
    public class TestGoogleDocumentStore : IDisposable {
        IDocument Root;
        string TmpFolder;
        IDocumentStore DocumentStore;

        public TestGoogleDocumentStore() {
			TmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var factory = new DocumentStoreFactory();
            DocumentStore = factory.LoadDocumentStore("google:///fsync-unittest");

            // TODO: Create with API directly
            Root = DocumentStore.Create(TmpFolder, DocumentType.Directory);
            /*
			var mockDriveService = new Moq.Mock<DriveService>();
			var mockChangesResource = new Moq.Mock<ChangesResource>(null);
			var mockStartTokenRequest = new Moq.Mock<ChangesResource.GetStartPageTokenRequest>(null);
			var mockStartToken = new Moq.Mock<StartPageToken>();

			mockDriveService.Setup(x => x.Changes).Returns(mockChangesResource.Object);
			mockChangesResource.Setup(x => x.GetStartPageToken()).Returns(mockStartTokenRequest.Object);
			mockStartTokenRequest.Setup(x => x.Execute()).Returns(mockStartToken.Object);
			mockStartToken.Setup(x => x.StartPageTokenValue).Returns("1");
			*/
        }

        public void Dispose() {
            Console.WriteLine("Removing {0}", Root.Id);
            // TODO: Remove with API directly
            DocumentStore.Delete(Root);
        }

        private void VerifyDocument(IDocument document) {
        }

        [Fact]
        public void TestCreate() {
            // Test path creation starting at root.
            VerifyDocument(DocumentStore.Create(System.IO.Path.Combine("/", TmpFolder, "FileA"), DocumentType.File));

            // Test path creation root inferred.
            VerifyDocument(DocumentStore.Create(System.IO.Path.Combine(TmpFolder, "FileB"), DocumentType.File));

            // Create Subdirectory
            var sub = DocumentStore.Create(Root, "SubA", DocumentType.Directory);

            DocumentStore.Create(sub, "FileA", DocumentType.File);

            // Create a Duplicate file.
            DocumentStore.Create(System.IO.Path.Combine(TmpFolder, "FileC"), DocumentType.File);
        }

        [Fact]
        public void TestDelete() {
            var root = DocumentStore.GetByPath(TmpFolder);
            var file = DocumentStore.Create(root, "FileA", DocumentType.File);

            DocumentStore.Delete(file);
        }

        [Fact]
        public void TestMoveTo_IDocument() {
            throw new Exception("stub");


        }

        [Fact]
        public void TestMoveTo_String() {
            throw new Exception("stub");
        }

        [Fact]
        public void GetByID() {
            IDocument document;
            document = DocumentStore.GetById(Root.Id);
            Assert.IsNotNull(document, "Unable to get Root Folder");

            Assert.DoesNotThrow(delegate {
                Assert.IsNull(DocumentStore.GetById("notfound"));
            });
        }

        [Fact]
        public void TestEventClassification() {
            DocumentChangeType eventType = DocumentChangeType.Changed;
            var watcher = DocumentStore.Watch();

            var changelog = DocumentStore.GetChangeLog();

            var doc = DocumentStore.Create(System.IO.Path.Combine("/", TmpFolder, "FileA"), DocumentType.File);

            foreach (var change in changelog) {
                var e = watcher.Classify(change);
                Console.WriteLine(e.ChangeType);
                eventType = e.ChangeType;
            }
            Assert.AreEqual(DocumentChangeType.Created, eventType);

            var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("Hello World"));
            doc.Update(stream);

            foreach (var change in changelog) {
                var e = watcher.Classify(change);
                Console.WriteLine(e.ChangeType);
                eventType = e.ChangeType;
            }
            Assert.AreEqual(DocumentChangeType.Changed, eventType);

            doc.Delete();
            foreach (var change in changelog) {
                var e = watcher.Classify(change);
                Console.WriteLine(e.ChangeType);
                eventType = e.ChangeType;
            }
            Assert.AreEqual(DocumentChangeType.Deleted, eventType);


            /*
			throw new NotImplementedException("Rename Events not implemented");
			foreach (var change in changelog) {
				var e = watcher.Classify(change);
				Console.WriteLine(e.ChangeType);
				Assert.AreEqual(DocumentChangeType.Deleted, e.ChangeType);
			}
			*/
        }

        [Fact]
        public void TestDocumentCache() {
            var dbname = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try {
                Console.WriteLine("DB: {0}", dbname);
                using (var db = new System.Data.SQLite.SQLiteConnection("Data Source=" + dbname)) {
                    // Test EntityFramework
                    var cache = new GoogleDocumentCache(db);
                    var index = new GoogleDocumentIndex { Id = "a", Parent = "b", Name = "c", Version = 4 };
                    cache.DocumentIndex.Add(index);
                    cache.SaveChanges();

                    var index2 = (from i in cache.DocumentIndex select i).First();
                    Assert.AreEqual(index, index2);
                    index.Version = 5;
                    cache.SaveChanges();

                    // test cache object
                    var document = DocumentStore.Create(Root, "test", DocumentType.File);

                    cache.Add(document);
                }


            }
            finally {
                //System.IO.File.Delete(dbname);
            }
        }

        [Fact]
        public void TestDriveList() {
        }

        [Fact]
        public void DriveWatcher() {
            var root = DocumentStore.GetByPath(TmpFolder);

            var watcher = DocumentStore.Watch();
            watcher.Path = root.Id;
            //watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*";

            watcher.EnableRaisingEvents = true;

            throw new Exception("stub");
        }

        [Fact]
        public void TestGetById() {
            DocumentStore.GetById(TmpFolder);
        }

        [Fact]
        public void TestGetByPath() {
			Assert.Null(DocumentStore.GetByPath("/non-existent"));

            // Expect null
            // Specify  root
            // Specify full path
        }

        [Fact]
        public void TestCopyAttributes() {
            throw NotImplementedException();
        }

        [Fact]
        public void TestUpdate_Attributes() {
            throw NotImplementedException();
        }

        [Fast]
        public void TestUpdate_Data() {
            throw NotImplementedException();
        }
    }
}