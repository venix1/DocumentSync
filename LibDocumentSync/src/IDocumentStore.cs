using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DocumentSync {
    public class DocumentException : SystemException {
        public DocumentException(string msg) : base(msg) { }
    }

    public class DocumentDoesNotExistException : DocumentException {
        public DocumentDoesNotExistException(string msg) : base(msg) { }
    }

    public enum DocumentType {
        File,
        Directory,
        Link,
    }

    public interface IDocument {
        IDocumentStore Owner { get; }
        string Id { get; }
        string Name { get; }
        string FullName { get; }
        DateTime CreatedTime { get; }
        DateTime ModifiedTime { get; set; }
        long Size { get; }
        long Version { get; }

        bool Deleted { get; }


        IDocument Parent { get; }
        bool Exists { get; }
        bool IsDirectory { get; }
        bool IsFile { get; }

        bool Trashed { get; }

        System.Collections.IEnumerable Children { get; }

        string Md5Checksum { get; }

        StreamReader OpenText();
        Stream OpenRead();

        // Operations
        void Update(Stream stream);
        void Delete();
    }

    public interface IDocumentEnumerable : IEnumerable<IDocument> {
    }

    public interface IDocumentEnumerator : IEnumerator<IDocument> {
    }

    public interface IDocumentStore : IEnumerable<IDocument> {
        IDocument Root { get; }
        IDocument CreateFile(string path, Stream stream);
        IDocument CreateFile(string path, string content);
        // IDocument CreateFile(IDocument parent, string name);

        /*
         * One line function bulk. 
        IDocument CreateDirectory(string path);
        IDocument CreateDirectory(IDocument parent, string name);
        */

        IDocument Create(string path, DocumentType type);
        IDocument Create(IDocument parent, string name, DocumentType type);
        Stream Open(IDocument document, System.IO.FileMode mode);
        bool Exists(string path);
        void Delete(IDocument arg0);
        void MoveTo(IDocument src, IDocument dst);
        void MoveTo(IDocument src, string name);

        void Update(IDocument dst);
        void Update(IDocument dst, Stream data);
        void Copy(IDocument src, IDocument dst);
        void CopyAttributes(IDocument src, IDocument dst);
        IDocument Clone(IDocument src, string dst);

        IDocument GetById(string id);
        IDocument GetByPath(string path);

        IEnumerable<IDocument> EnumerateFiles(string path = "/", string filter = "*", SearchOption options = SearchOption.AllDirectories);
        IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "*", SearchOption options = SearchOption.AllDirectories);
        IEnumerable<IDocument> List(IDocument document = null);

        DocumentWatcher Watch();
    }

    public abstract class DocumentStore : IDocumentStore {
        // abstract primitives
        public abstract IDocument Root { get; }
        public abstract Stream Open(IDocument document, System.IO.FileMode mode);
        public abstract IDocument Create(string path, DocumentType type);
        public abstract IDocument Create(IDocument parent, string name, DocumentType type);

        public abstract void Update(IDocument dst);
        public abstract void Delete(IDocument arg0);
        public abstract IDocument GetById(string id);
        public abstract IDocument GetByPath(string path);

        public bool Exists(string path) => GetByPath(path) != null;
        public abstract void MoveTo(IDocument src, IDocument dst);
        public abstract void MoveTo(IDocument src, string name);
        public abstract void Update(IDocument dst, Stream stream);
        abstract public IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "*", SearchOption options = SearchOption.AllDirectories);

        public abstract DocumentWatcher Watch();

        public abstract IEnumerator<IDocument> GetEnumerator();

        // generic wrappers
        public IDocument CreateFile(string path, Stream stream) {
            Console.WriteLine("Creating {0}", path);
            if (Exists(path))
                throw new DocumentException("File exists");
            var document = Create(path, DocumentType.File);
            document.Update(stream);
            return document;
        }
        public IDocument CreateFile(string path, string content) {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            return CreateFile(path, stream);
        }

        public void CopyAttributes(IDocument src, IDocument dst) {
            dst.ModifiedTime = src.ModifiedTime;
        }
        public void Copy(IDocument src, IDocument dst) {
            using (var fp = src.OpenRead()) {
                Update(dst, fp);
            }
            CopyAttributes(src, dst);
        }

        public IDocument Clone(IDocument src, string dst) {
            DocumentType docType;
            if (src.IsDirectory) {
                docType = DocumentType.Directory;
            }
            else if (src.IsFile) {
                docType = DocumentType.File;
            }
            else { throw new NotImplementedException(); }
            var newDoc = Create(dst, docType);
            Copy(src, newDoc);
            CopyAttributes(src, newDoc);
            return newDoc;
        }

        public IEnumerable<IDocument> EnumerateFiles(string path = "/", string filter = "*", SearchOption options = SearchOption.AllDirectories) {
            var document = GetByPath(path);
            return EnumerateFiles(document, filter, options);
        }


        // TODO: Refactor out and use IDocument for implementation
        public IEnumerable<IDocument> GetContents(IDocument document) {
            throw new NotImplementedException();
        }
        public IEnumerable<IDocument> List(IDocument document = null) {
            /*
             * 			foreach (var document in EnumerateFiles("/", "", SearchOption.AllDirectories)) {
                yield return document;
            }
            */
            // return Root.GetEnumerator();
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public enum DocumentChangeType {
        All,
        Changed,
        Created,
        Deleted,
        Renamed
    }

    public class DocumentEventArgs : EventArgs {
        public DocumentChangeType ChangeType { get; }
        public IDocument Document { get; }
        // public IDocument Original { get; }

        public DocumentEventArgs(DocumentChangeType type, IDocument document) {
            ChangeType = type;
            Document = document;
        }
    }

    public delegate void DocumentEventHandler(
        object sender,
        DocumentEventArgs e
    );

    public enum NotifyFilters {
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
        public Thread PollThread { get; protected set; }
        public abstract DocumentEventArgs Classify(IDocument change);

        public bool PauseRaisingEvents { get; set; }
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
