using System;
using System.IO;

namespace FSync
{
	public class DocumentException : Exception {
	}

	public class DocumentDoesNotExistException : Exception
	{
	}

	public enum DocumentType
	{
		File,
		Directory,
		Link,
	}
	
	public interface IDocument
	{
		string Id { get; }
		string Name { get; }
		string FullName { get; }

		IDocument Parent { get; }
		bool Exists { get; }
		bool IsDirectory { get; }
		bool IsFile { get; }

		System.Collections.IEnumerable Children { get; }

		string Md5Checksum { get; }
	}

	public interface IDocumentStore
	{
		/*
		 * One line function bulk. 
		IDocument CreateFile(string path);
		IDocument CreateFile(IDocument parent, string name);

		IDocument CreateDirectory(string path);
		IDocument CreateDirectory(IDocument parent, string name);
		*/

		IDocument Create(string path, DocumentType type);
		IDocument Create(IDocument parent, string name, DocumentType type);

		void Delete(IDocument arg0);
		void MoveTo(IDocument src, IDocument dst);
		void MoveTo(IDocument src, string name);

		IDocument GetById(string id);
		IDocument GetByPath(string path);

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