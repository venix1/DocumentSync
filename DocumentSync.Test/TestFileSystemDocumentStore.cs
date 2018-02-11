using NUnit.Framework;

using System;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;

namespace DocumentSync.Test
{
	public class TestFileSystemDocumentStore
	{
		Moq.Mock<DriveService> mockDriveService;
		string TmpFolder;
		DirectoryInfo Root;
		FileSystemDocumentStore DocumentStore;

		public TestFileSystemDocumentStore()
		{
			DocumentStore = new FileSystemDocumentStore("/");
		}

		[SetUp]
		protected void SetUp()
		{
			TmpFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Root = new DirectoryInfo(TmpFolder);
			Console.WriteLine("Creating {0}", Root.FullName);
			Root.Create();
		}

		[TearDown]
		protected void Cleanup()
		{
			Console.WriteLine("Removing {0}", Root.FullName);
			Root.Delete(true);
		}

		private void VerifyDocument(IDocument document)
		{
		}

		[Test]
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

		[Test]
		public void TestDelete()
		{
			var root = DocumentStore.GetById(TmpFolder);
			var file = DocumentStore.Create(root, "FileA", DocumentType.File);

			DocumentStore.Delete(file);
		}

		[Test]
		public void TestMoveTo_IDocument()
		{
			throw new Exception("stub");


		}

		[Test]
		public void TestMoveTo_String()
		{
			throw new Exception("stub");
		}

		[Test]
		public void GetById()
		{
			DocumentStore.GetById(TmpFolder);
		}
	}
}

