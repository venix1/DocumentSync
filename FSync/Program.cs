using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;
using System;

using System.Diagnostics;
using System.Security.Cryptography;
using System.IO;
using System.Threading;

using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace FSync
{
class DocumentSync
	{
		IDocumentStore[] DocumentStores { get; set;}
		public DocumentSync(params IDocumentStore[] documents)
		{
		}

		public void Converge()
		{
			// Get Documents in A, store in path, IDocument hash 
			// Get Documents in B, store in path, IDocument hash

		}

		private void ProcessQueue(Object source, System.Timers.ElapsedEventArgs e)
		{
			/*
			try {
				GetDriveChanges();

				var queue = SyncQueue;
				SyncQueue = new Queue<EventArgs>();

				Console.WriteLine("Executing sync queue. {0}", queue.Count);
				foreach (var change in queue) {
					Console.WriteLine("Change: {0}", change.GetType());

					if (change is DriveChangedEventArgs)
						ExecuteDriveChangedEvent((DriveChangedEventArgs)change);
					else if (change is DriveCreatedEventArgs)
						ExecuteDriveCreatedEvent((DriveCreatedEventArgs)change);
					else if (change is DriveDeletedEventArgs)
						ExecuteDriveDeletedEvent((DriveDeletedEventArgs)change);
					else if (change is DriveRenamedEventArgs)
						ExecuteDriveRenamedEvent((DriveRenamedEventArgs)change);

					else if (change is FileChangedEventArgs)
						ExecuteFileChangedEvent((FileChangedEventArgs)change);
					else if (change is FileCreatedEventArgs)
						ExecuteFileCreatedEvent((FileCreatedEventArgs)change);
					else if (change is FileDeletedEventArgs)
						ExecuteFileDeletedEvent((FileDeletedEventArgs)change);
					else if (change is FileRenamedEventArgs)
						ExecuteFileRenamedEvent((FileRenamedEventArgs)change);
					else { } // throw new Exception("Unknown Event type");

				}
			} catch (Exception ex) {
				Console.WriteLine(ex);
			}
			*/
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			if (args.Length != 2)
			{
				Console.WriteLine("Usage: Watcher.exe <drive folder> <directory>");
				return;
			}

			var driveStore = new GoogleDriveDocumentStore(args[0]);
			var fileStore = new FileSystemDocumentStore(args[1]);
			var program = new DocumentSync(driveStore, fileStore);
			program.Converge();
		}
	}
}