using System;
namespace FSync
{
	public class GoogleDriveDocument : IDocument
	{
	}

	public class GoogleDriveDocumentStore : IDocumentStore
	{
		public GoogleDriveDocumentStore()
		{
		}
	}

	public class GoogleDriveDocumentWatcher : DocumentWatcher
	{
		private string savedStartPageToke;

		private void Check()
		{
			string pageToken = savedStartPageToken;
			while (pageToken != null) {
				var request = DriveService.Changes.List(pageToken);

				request.IncludeRemoved = true;
				request.Fields = "changes(file(id,md5Checksum,mimeType,modifiedTime,name,parents,size,trashed,version),fileId,removed,time),newStartPageToken,nextPageToken";
				request.Spaces = "drive";

				var changes = request.Execute();
				foreach (var change in changes.Changes) {
					EventArgs syncEvent = null;

					if (change.Removed.GetValueOrDefault(false) || change.File.Trashed.GetValueOrDefault(false)) {
						Console.WriteLine("Removed: {0} {1}", change.FileId, change.File);
						syncEvent = new DriveDeletedEventArgs(GetUnifiedFile(change.FileId));
					} else {
						UnifiedFile unifiedFile = GetUnifiedFileById(change.FileId);

						if (unifiedFile == null)
							syncEvent = new DriveCreatedEventArgs(unifiedFile);
						else if (unifiedFile.File.Name != change.File.Name)
							syncEvent = new DriveRenamedEventArgs(unifiedFile, GetUnifiedFile(change.File));
						else
							syncEvent = new DriveChangedEventArgs(unifiedFile);
					}
					Console.WriteLine("{0} {1} {2}", syncEvent, change.FileId, (change.File != null) ? change.File.Name : null);
					SyncQueue.Enqueue(syncEvent);
				}
				if (changes.NewStartPageToken != null) {
					Console.WriteLine("NewStartPageToken: {0}", changes.NewStartPageToken);
					// Last page, save this token for the next polling interval
					savedStartPageToken = changes.NewStartPageToken;
				}
				if (changes.NextPageToken != null) {
					Console.WriteLine("NextPageToken: {0}", changes.NextPageToken);
				}
				pageToken = changes.NextPageToken;
			}
		}
	}
}

