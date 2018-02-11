using System;
using System.Collections.Generic;

namespace DocumentSync
{
class DocumentSync
	{
		IDocumentStore[] DocumentStores { get; set; }
		IDocumentStore PrimaryDocumentStore { get; set; }
		public DocumentSync(params IDocumentStore[] documents)
		{
			DocumentStores = documents;
		}

		public void Converge()
		{
			var documents = new Dictionary<String, IDocument>();

			foreach (IDocument document in DocumentStores[0].List()) {
				documents.Add(document.FullName, document);
			}
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