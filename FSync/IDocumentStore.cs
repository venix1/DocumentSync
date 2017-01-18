using System;
using System.IO;

namespace FSync
{
	
	public interface IDocument
	{
		string ID { get; }
		string Name { get; }

		IDocumentStore Parent { get; }
		bool Exists { get; }
		bool IsDirectory { get; }
		bool IsFile { get; }

		string Md5Checksum { get; }
	}

	public interface IDocumentStore
	{
		IDocument Create();
		void Delete(IDocument arg0);
		void MoveTo(IDocument src, IDocument dst);
		void MoveTo(IDocument src, string name);
	}

	public enum DocumentChangeType
	{
		All,
		Changed,
		Created,
		Deleted,
		Renamed
	}

	public abstract class DocumentEventArgs : EventArgs
	{
		DocumentChangeType ChangeType { get; }
		IDocumentStore Target { get; }
		IDocumentStore Original { get; }
	}

	public delegate void DocumentEventHandler(
		object sender,
		DocumentEventArgs e
	);

	public enum NotifyFilters
	{
		Attributes,
		CreationTime,
		DirectoryName,
		FileName,
		LastAccess,
		LastWrite,
		Security,
		Size
	}

	public abstract class DocumentWatcher
	{
		IDocument Document { get; set; }
		NotifyFilters NotifyFilter { get; set; }
		string Filter { get; set;}

		DocumentEventHandler Changed;
		DocumentEventHandler Created;
		DocumentEventHandler Deleted;
		DocumentEventHandler Renamed;
	}
}