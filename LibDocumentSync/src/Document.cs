using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DocumentSync {
    public abstract class Document : IDocument {
        public DateTime CreationTime =>  CreationTimeUtc.ToLocalTime();
        public abstract DateTime CreationTimeUtc { get; }
        public abstract IDocument Directory { get; }
        public string DirectoryName => Path.GetDirectoryName(FullName);
        public abstract DocumentType DocumentType { get; }
        public abstract bool Exists { get; }
        public string Extension => Path.GetExtension(Name);
        public abstract string FullName { get; }
        public abstract string Id { get; }
        public bool IsDirectory => DocumentType == DocumentType.Directory;
        public bool IsFile => DocumentType == DocumentType.File;
        public abstract bool IsReadOnly { get; set; }
        public DateTime LastModifiedTime {
            get {
                return LastModifiedTimeUtc.ToLocalTime();
            }
            set {
                LastModifiedTimeUtc = value.ToUniversalTime();
            }
        }
        public abstract DateTime LastModifiedTimeUtc { get; set; }
        public abstract long Length { get; }
        public abstract string Md5sum { get; }
        public abstract string Name { get; }
        public IDocumentStore Owner { get; protected set; }
        public abstract bool Trashed { get; }
        public abstract long Version { get; }

        public StreamWriter AppendText() => new StreamWriter(Open(FileMode.Append));
        public IDocument CopyTo(IDocument dst, bool overwrite = false) {
            throw new NotImplementedException();
        }
        // public abstract void Create(DocumentType type);
        // public FileStream Create() => Open(FileMode.Create, FileAccess.ReadWrite);
        // public StreamWriter CreateText() => new StreamWriter(Open(FileMode.Create, FileAccess.Write));
        public void Delete() => Owner.Delete(this);
        public IEnumerable<IDocument> EnumerateDocuments() {
            throw new NotImplementedException();
        }
        public List<IDocument> GetDocuments(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) {
            throw new NotImplementedException();
        }
        public Stream Open(FileMode mode = FileMode.Open, FileAccess access=FileAccess.Read, FileShare share=FileShare.Read) {
            return Owner.Open(this, mode, access, share);
        }
        public Stream OpenRead() => Open(FileMode.Open);
        public StreamReader OpenText() => new StreamReader(OpenRead());
        public Stream OpenWrite() => Open(FileMode.Create);
        public void Replace(IDocument dst, string backupName, bool ignoreMetadataErrors) {
            throw new NotImplementedException();
        }
    }
}