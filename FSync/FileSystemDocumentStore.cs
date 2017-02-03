using System;
using System.IO;

namespace FSync
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

		public IDocument Parent { get { throw new Exception("stub"); } }
		public bool Exists {
			get { throw new Exception("stub"); }
		}
		public bool Trashed {
			get {
				throw new Exception("stub");
			}
		}

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
	}

	public class FileSystemDocumentStore : IDocumentStore
	{
		public FileSystemDocumentStore()
		{
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
	}

	public class FileSystemDocumentWatcher : DocumentWatcher
	{
		public FileSystemDocumentWatcher()
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
	}
}

