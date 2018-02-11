using System;
using System.Collections.Generic;
using System.IO;

namespace DocumentSync
{
	public class FileSystemDocument : IDocument
	{
		FileSystemDocumentStore Owner { get; set; }
		FileSystemInfo Document { get; set; }

		FileInfo FileInfo { get { return Document as FileInfo; } set { Document = value; } }
		DirectoryInfo DirectoryInfo { get { return Document as DirectoryInfo; } set { Document = value; } }

		internal FileSystemDocument(FileSystemDocumentStore owner, FileSystemInfo fsi)
		{
			Owner = owner;
			Document = fsi;
		}

		// IDocument Interface

		public string Id { get { return Document.FullName; } }
		public string Name { get { return Document.Name; } }
		public string FullName { get { return Document.FullName; } }
		public DateTime CreatedTime { get { return Document.CreationTime; }}
		public DateTime ModifiedTime { get { return Document.LastWriteTime; } }
		public IDocument Parent { get { throw new NotImplementedException(); } }
		public long Version { get { throw new NotImplementedException(); } }
		public bool Deleted { get { throw new NotImplementedException(); } }
		public bool Exists { get { throw new NotImplementedException(); } }
		public bool Trashed { get { throw new NotImplementedException(); } }

		public bool IsDirectory {
			get {
				return DirectoryInfo != null;
			}
		}

		public bool IsFile {
			get {
				return FileInfo != null;
			}
		}

		public System.Collections.IEnumerable Children {
				get { throw new Exception("stub"); }
			}

		public string Md5Checksum {
				get { throw new Exception("stub"); }
			}

		public void Update(System.IO.Stream stream)
		{
			throw new NotImplementedException("File Modifications not implemented");
		}
		public void Delete()
		{
			throw new NotImplementedException("File Modifications not implemented");
		}
	}

	public class FileSystemDocumentStore : IDocumentStore
	{
		public FileSystemDocumentStore(string root)
		{
			throw new Exception("Unable to find root folder");
		}

		public IDocument Create(string path, DocumentType type)
		{
			var tail = System.IO.Path.GetFileName(path);
			var head = System.IO.Path.GetDirectoryName(path);

			return Create(GetByPath(head), tail, type);
		}

		public IDocument Create(IDocument parent, string name, DocumentType type)
		{
			FileSystemInfo fsi;
			var path = Path.Combine(parent.FullName, name);

			switch (type) {
				case DocumentType.File:
					var fi = new FileInfo(path);
					fi.Create();
					fsi = fi;
					break;
				case DocumentType.Directory:
					var di = new DirectoryInfo(path);
					di.Create();
					fsi = di;
					break;
				default:
					throw new Exception("DocumentType not implemented");
			}
			return new FileSystemDocument(this, fsi);
		}

		public void Delete(IDocument arg0)
		{
			if (arg0.IsFile)
				File.Delete(arg0.FullName);

			if (arg0.IsDirectory)
				Directory.Delete(arg0.FullName, true);
		}

		public void MoveTo(IDocument src, IDocument dst)
		{
			throw new Exception("stub");
		}
		public void MoveTo(IDocument src, string name)
		{
			throw new Exception("stub");
		}

		public IDocument GetById(string id)
		{
			// For now, id and Path are the same.
			return GetByPath(id);
		}

		public IDocument GetByPath(string path)
		{
			FileSystemInfo fi;
			if (System.IO.Directory.Exists(path))
				fi = new DirectoryInfo(path);
			else if (System.IO.File.Exists(path))
				fi = new FileInfo(path);
			else
				throw new Exception("File does not exist.");

			return new FileSystemDocument(this, fi);
		} 

		public DocumentWatcher Watch()
		{
			return new FileSystemDocumentWatcher();
		}

		public IEnumerable<IDocument> GetContents(IDocument document)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<IDocument> EnumerateFiles(string path, string filter, SearchOption options)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<IDocument> List()
		{
			throw new NotImplementedException();
		}
	}

	public class FileSystemDocumentWatcher : DocumentWatcher
	{
		Queue<EventArgs> SyncQueue { get; set; }

		internal FileSystemDocumentWatcher()
		{
			/*
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
			*/
		}

		public override DocumentEventArgs Classify(IDocument change)
		{
			throw new NotImplementedException();
		}
		private void OnChanged(object source, FileSystemEventArgs e)
		{
			lock (SyncQueue) {
				/*
				try {
					Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
					EventArgs syncEvent = null;
					UnifiedFile unifiedFile = GetUnifiedFile(e.FullPath);
					switch (e.ChangeType) {
						case WatcherChangeTypes.Changed:
							if (!System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileChangedEventArgs(unifiedFile);
							break;
						case WatcherChangeTypes.Created:
							if (!System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileCreatedEventArgs(unifiedFile);
							break;
						case WatcherChangeTypes.Deleted:
							if (System.IO.File.Exists(e.FullPath)) {
								Console.WriteLine("Out of Order event, Discarding");
								break;
							}
							syncEvent = new FileDeletedEventArgs(unifiedFile);
							break;
						default:
							throw new Exception("unhandled ChangeType");
					}
					if (syncEvent != null)
						SyncQueue.Enqueue(syncEvent);
				} catch (Exception ex) {
					Console.WriteLine(ex);
				}
				*/
			}
		}

		private void OnRenamed(object source, RenamedEventArgs e)
		{
			/*
			lock (SyncQueue) {
				Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
				SyncQueue.Enqueue(new FileRenamedEventArgs(e.OldFullPath, GetUnifiedFile(e.FullPath)));
			}
			*/
		}
	}
}

