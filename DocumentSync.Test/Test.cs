
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using GoogleFile = Google.Apis.Drive.v3.Data.File;
using System.IO;
using Xunit;

namespace DocumentSync.Test
{
	public class FileSystemInodeTest
	{
	}

	public class UnifiedFileTest
	{
	}

	public class UnifiedFileSystemTest
	{
		GoogleFile DriveRoot;
		FileSystemInfo FileRoot;

		public UnifiedFileSystemTest() {

			DriveRoot = new GoogleFile {
			};

			var mock = new Moq.Mock<FileSystemInfo>();
			FileRoot = mock.Object;

			FileRoot = new DirectoryInfo("/");
		}

		[Fact]
		public void GetDriveFileInfo_() {
			var mockDriveService = new Moq.Mock<DriveService>();
			var mockChangesResource = new Moq.Mock<ChangesResource>(null);
			var mockStartTokenRequest = new Moq.Mock<ChangesResource.GetStartPageTokenRequest>(null);
			var mockStartToken = new Moq.Mock<StartPageToken>();

			mockDriveService.Setup(x => x.Changes).Returns(mockChangesResource.Object);
			mockChangesResource.Setup(x => x.GetStartPageToken()).Returns(mockStartTokenRequest.Object);
			mockStartTokenRequest.Setup(x => x.Execute()).Returns(mockStartToken.Object);
			mockStartToken.Setup(x => x.StartPageTokenValue).Returns("1");
			// DriveService.Changes.GetStartPageToken().Execute();
			//var unifiedFileSystem = new UnifiedFileSystem(mockDriveService.Object, DriveRoot, FileRoot);
			//unifiedFileSystem.GetDriveFileInfo("0b01");
			// Google.Apis.Drive.v3.Data.File GetDriveFileInfo(string id)
		}
		public void GetDriveFileParent_() { 
			// Google.Apis.Drive.v3.Data.File GetDriveFileParent(Google.Apis.Drive.v3.Data.File file)
		}
		public void GetDrivePath_() { }
		public void GetUnifiedFile_() { }
		//Google.Apis.Drive.v3.Data.File
		//FileSystemInfo
		//FileSystemInode
		//"/file"
		//"0BXXX"
		public void GetUnifiedFileById_() { }

		public void BuildLocalPath_() { }
		public void BuildRelativePath_() { }

		public void AddFile_() { }
		// (Google.Apis.Drive.v3.Data.File file)
		// FileSystemInfo fileSystemInfo)
		//(UnifiedFile file)

		public void TestCreateExisting() {
			// Tests creation of existinf ile.  Must throw exception.
		}

		public void GetFileByPath_() { }
		public void ScanDisk_() { }
		public void ScanDrive_() { }

		public void Converge_() { }
		public void SynchronizeFile_() { }

		public void GetDriveChanges_() { }

		public void ExecuteDriveChangedEvent_() { 
		}
		public void ExecuteDriveCreatedEvent_() { }
		public void ExecuteDriveDeletedEvent_() { }
		public void ExecuteDriveRenamedEvent_() { }
		public void ExecuteFileChangedEvent_() { }
		public void ExecuteFileCreatedEvent_() { }
		public void ExecuteFileDeletedEvent_() { }
		public void ExecuteFileRenamedEvent_() { }

		public void OnChanged_() { }
		public void OnRenamed_() { }


		public void Event_LocalFileCreated() { }
		public void Event_LocalFileChanged() { }
		public void Event_LocalFileRemoved() { }
		public void Event_LocalFileRenamedInFolder() { }
		public void Event_LocalFileMovedSubFolder() { }
		public void Event_LocalFileMovedParentFolder() { }
		public void Event_LocalFileMovedSiblingFolder() { }

		public void Event_RemoteFileCreated() { }
		public void Event_RemoteFileChanged() { }
		public void Event_RemoteFileRemoved() { }
		public void Event_RemoteFileRenamedInFolder() { }
		public void Event_RemoteFileMovedSubFolder() { }
		public void Event_RemoteFileMovedParentFolder() { }
		public void Event_RemoteFileMovedSiblingFolder() { }

		public void ConsolidateEventsChangeChange() { }
		public void ConsolidateEventsChangeDelete() { }
		public void ConsolidateEventsCreateDelete() { }

		// NullReference with "touch test; rm test"		                          
		public void TestEventsCreateChangeDelete() { }
		public void TestEventsDeleteChangedCreate() { }
	}
}

