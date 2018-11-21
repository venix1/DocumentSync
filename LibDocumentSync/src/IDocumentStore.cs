using System;
using System.Collections.Generic;
using System.IO;

namespace DocumentSync
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
        DateTime CreatedTime { get; }
        DateTime ModifiedTime { get; }
        long Version { get; }

        bool Deleted { get; }


        IDocument Parent { get; }
        bool Exists { get; }
        bool IsDirectory { get; }
        bool IsFile { get; }



        bool Trashed { get; }

        System.Collections.IEnumerable Children { get; }

        string Md5Checksum { get; }

        // Operations
        void Update(System.IO.Stream stream);
        void Delete();
    }

    public interface IDocumentEnumerable : IEnumerable<IDocument>
    {
    }

    public interface IDocumentEnumerator : IEnumerator<IDocument>
    {
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

        IEnumerable<IDocument> GetContents(IDocument document);
        IEnumerable<IDocument> EnumerateFiles(string path, string filter, SearchOption options);

        IEnumerable<IDocument> List();

        DocumentWatcher Watch();
    }

    public enum DocumentChangeType
    {
        All,
        Changed,
        Created,
        Deleted,
        Renamed
    }

    public class DocumentEventArgs : EventArgs
    {
        public DocumentChangeType ChangeType { get; }
        public IDocument Document { get; }
        // public IDocument Original { get; }

        public DocumentEventArgs(DocumentChangeType type, IDocument document)
        {
            ChangeType = type;
            Document = document;
        }
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

    public abstract class DocumentWatcher // : System.ComponentModel.Component
    {
        public abstract DocumentEventArgs Classify(IDocument change);

        public bool EnableRaisingEvents { get; set; }
        public bool IncludeSubdirectories { get; set; }

        public string Filter { get; set; }
        public NotifyFilters NotifyFilter { get; set; }
        public string Path { get; set; }

        public DocumentEventHandler Changed;
        public DocumentEventHandler Created;
        public DocumentEventHandler Deleted;
        public DocumentEventHandler Renamed;
    }
}