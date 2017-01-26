using NUnit.Framework;

using System;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;

namespace FSync.Test
{
	public class TestGoogleDocumentStore
	{
		Moq.Mock<DriveService> mockDriveService;
		IDocument Root;
		string TmpFolder;
		GoogleDriveDocumentStore DocumentStore;

		public TestGoogleDocumentStore()
		{
			DocumentStore = new GoogleDriveDocumentStore();
		}

		[SetUp]
		protected void SetUp()
		{
			TmpFolder = System.IO.Path.GetRandomFileName();

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

		[TearDown]
		protected void Cleanup()
		{
			Console.WriteLine("Removing {0}", Root.Id);
			// TODO: Remove with API directly
			DocumentStore.Delete(Root);
		}

		private void VerifyDocument(IDocument document)
		{
		}

		[Test]
		public void TestCreate()
		{
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

		[Test]
		public void TestDelete()
		{
			var root = DocumentStore.GetByPath(TmpFolder);
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
		public void GetByID()
		{
			throw new Exception("stub");
		}
	}
}

