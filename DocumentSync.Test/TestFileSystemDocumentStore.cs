using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using System;
using System.IO;
using Xunit;

namespace DocumentSync.Test
{
	public class TestFileSystemDocumentStore : IDisposable
	{
		// Moq.Mock<DriveService> mockDriveService;
		string TmpFolder;
		DirectoryInfo Root;
		IDocumentStore DocumentStore;

        public TestFileSystemDocumentStore() {
            TmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Root = new DirectoryInfo(TmpFolder);
            Console.WriteLine("Creating {0}", Root.FullName);
            Root.Create();

            var factory = new DocumentStoreFactory();
            DocumentStore = factory.LoadDocumentStore("fs://" + TmpFolder);
        }

        public void Dispose()
		{
			Console.WriteLine("Removing {0}", Root.FullName);
			Root.Delete(true);
		}

		private void VerifyDocument(IDocument document)
		{
		}

		[Fact]
		public void TestCreate()
		{
			var root  = DocumentStore.GetById(TmpFolder);

			// Test path creation root inferred.
			VerifyDocument(DocumentStore.Create(System.IO.Path.Combine(TmpFolder, "FileA"), DocumentType.File));

			VerifyDocument(DocumentStore.Create(root, "FileB", DocumentType.File));

			// Make a sub directory
			VerifyDocument(DocumentStore.Create(root, "SubA", DocumentType.Directory));

			// Create a Duplicate file.
			VerifyDocument(DocumentStore.Create(System.IO.Path.Combine(TmpFolder, "SubA/FileA"), DocumentType.File));
		}


		[Fact]
		public void TestDelete()
		{
			var root = DocumentStore.GetById(TmpFolder);
			var file = DocumentStore.Create(root, "FileA", DocumentType.File);

			DocumentStore.Delete(file);
		}

		[Fact]
		public void TestMoveTo_IDocument()
		{
			throw new Exception("stub");


		}

		[Fact]
		public void TestMoveTo_String()
		{
			throw new Exception("stub");
		}

		[Fact]
		public void TestGetById()
		{
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

		[Fast]
		public void Test_InvalidOwner() {
            throw new NotImplementedException();
        }

	}
}

