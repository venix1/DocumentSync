using System;
namespace FSync
{
	public class FileSystemDocument : IDocument
	{
	}

	public class FileSystemDocumentStore : IDocumentStore
	{
		public FileSystemDocumentStore()
		{
		}
	}

	public class FileSystemDocumentWatcher : DocumentWatcher
	{
		public FileSystemDocumentWatcher()
		{
			var timer = new System.Timers.Timer(5000);
			timer.AutoReset = true;
			timer.Elapsed += ProcessQueue;
			timer.Start();

			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = Root.Inode.FullName;
			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.Filter = "";

			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnChanged);
			watcher.Deleted += new FileSystemEventHandler(OnChanged);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);

			watcher.EnableRaisingEvents = true;
		}
	}
}

