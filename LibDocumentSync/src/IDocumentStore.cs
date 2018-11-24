using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocumentSync {
    public class DocumentException : Exception {
    }

    public class DocumentDoesNotExistException : DocumentException {
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

        void Delete(IDocument arg0);
        void MoveTo(IDocument src, IDocument dst);
        void MoveTo(IDocument src, string name);

        void Update(IDocument src, Stream data);
        void Copy(IDocument src, IDocument dst);
        IDocument Clone(IDocument src, string dst);

        IDocument GetById(string id);
        IDocument GetByPath(string path);

        IEnumerable<IDocument> EnumerateFiles(string path = "/", string filter = "*", SearchOption options = SearchOption.AllDirectories);
        IEnumerable<IDocument> List(IDocument document=null);

        DocumentWatcher Watch();
    }

    public abstract class DocumentStore : IDocumentStore {
        public abstract IDocument Create(string path, DocumentType type);
        public abstract IDocument Create(IDocument parent, string name, DocumentType type);
        public IDocument CreateFile(string path, Stream stream) {
            Console.WriteLine("Creating {0}", path);
            var document = Create(path, DocumentType.File);
            document.Update(stream);
            return document;
        }
        public IDocument CreateFile(string path, string content) {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            return CreateFile(path, stream);
        }
        public abstract void Update(IDocument src, Stream stream);
        public abstract void Copy(IDocument src, IDocument dst);
        public abstract void Delete(IDocument arg0);
        public abstract IDocument GetById(string id);
        public abstract IDocument GetByPath(string path);
        public abstract void MoveTo(IDocument src, IDocument dst);
        public abstract void MoveTo(IDocument src, string name);
        public abstract DocumentWatcher Watch();

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
            CopyMetadata(src, newDoc);
            return newDoc;
        }
        public void CopyMetadata(IDocument src, IDocument dst) {
            dst.ModifiedTime = src.ModifiedTime;
        }

        /** Naive implememntation.  Can be optimized per Store as required **/
        abstract public IEnumerable<IDocument> EnumerateFiles(string path = "/", string filter = "*", SearchOption options = SearchOption.AllDirectories);

        // TODO: Refactor out and use IDocument for implementation
        public IEnumerable<IDocument> GetContents(IDocument document) {
            throw new NotImplementedException();
        }
        public IEnumerable<IDocument> List(IDocument document=null) {
            /*
             * 			foreach (var document in EnumerateFiles("/", "", SearchOption.AllDirectories)) {
                yield return document;
            }
            */
            // return Root.GetEnumerator();
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public abstract IEnumerator<IDocument> GetEnumerator();
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
